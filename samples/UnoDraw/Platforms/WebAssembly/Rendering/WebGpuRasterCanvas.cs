using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Silk.NET.WebGPU;
using SkiaSharp;
using Svg.Editor.Skia.Uno;
using Uno.WinUI.Graphics3DWebGpu;

namespace UnoDraw.Rendering;

/// <summary>
/// Keeps the existing BGRA Skia raster path, swaps red/blue with Skia's native
/// byte permutation into a reusable buffer, and uploads the resulting RGBA pixels
/// into WebGpuCanvasElement's persistent texture. Device scale, premultiplied
/// alpha and the painter's original antialiasing settings are retained.
/// </summary>
internal sealed unsafe class WebGpuRasterCanvas : WebGpuCanvasElement, ISkiaCanvasIslandTarget
{
    private readonly Action<string?> _fallback;
    private readonly ScaleTransform _displayScale = new();
    private CanvasPixelSize _pixels;
    private readonly SkiaCanvasRasterState _raster = new();
    private ISkiaCanvasLayers? _compositeLayers;
    private bool _documentDirty = true;
    private bool _reportedCompositeReuse;
    private uint _maximumTextureSize;
    private IDisposable? _deviceLifetime;

    internal WebGpuRasterCanvas(Action<string?> fallback)
    {
        _fallback = fallback;
        IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;

        // Uno 7.0.32's WebGpuCanvasElement sizes its target from logical RenderSize
        // and omits XamlRoot.RasterizationScale. Express this transport child in
        // integer physical pixels, then map it back to the parent's logical size.
        // A second layout rounding at fractional DPR would round physical pixels
        // as if they were logical units, so disable it only on this private child.
        UseLayoutRounding = false;
        RenderTransform = _displayScale;
        RegisterPropertyChangedCallback(IsWebGpuInitializedProperty, (_, _) =>
        {
            if (IsLoaded && IsWebGpuInitialized == false)
                _fallback("the WebGPU canvas could not initialize or render");
        });
    }

    public FrameworkElement Element => this;
    public ISkiaCanvasRenderer? Renderer { get; set; }
    public void InvalidateInteraction() => Invalidate();

    public void SetCompositeLayers(ISkiaCanvasLayers? layers)
    {
        if (ReferenceEquals(_compositeLayers, layers))
            return;
        _compositeLayers = layers;
        ReleaseDocumentCache();
        Invalidate();
    }

    // Ordinary SKCanvasElement invalidation reaches the island here, including
    // calls made through its nonvirtual base API. Feedback uses Invalidate directly.
    public void InvalidateDocument()
    {
        _documentDirty = true;
        Invalidate();
    }

    public bool Configure(CanvasPixelSize pixels)
    {
        if (_maximumTextureSize != 0 &&
            (pixels.Width > _maximumTextureSize || pixels.Height > _maximumTextureSize))
        {
            _fallback("the canvas exceeds the WebGPU device's texture limit");
            return false;
        }

        if (_pixels == pixels)
            return true;
        ReleaseDocumentCache();
        _pixels = pixels;
        Width = pixels.Width;
        Height = pixels.Height;
        _displayScale.ScaleX = _displayScale.ScaleY = 1 / pixels.Scale;
        Invalidate();
        return true;
    }

    protected override void Init(WebGpuCanvasContext context)
    {
        try
        {
            if (context.TargetFormat != TextureFormat.Rgba8Unorm)
                throw new NotSupportedException("The canvas bridge requires an RGBA8 WebGPU target.");

            SupportedLimits limits = default;
            if (!context.Api.DeviceGetLimits(context.Device, &limits))
                throw new NotSupportedException("The WebGPU device limits are unavailable.");
            _maximumTextureSize = limits.Limits.MaxTextureDimension2D;
            if (_pixels.Width > _maximumTextureSize || _pixels.Height > _maximumTextureSize)
                throw new NotSupportedException("The canvas exceeds the WebGPU device's texture limit.");

            _deviceLifetime = WebGpuDeviceLifetime.Subscribe((nint)context.Device, this);
        }
        catch
        {
            // Uno has not committed this context yet and does not call OnDestroy
            // if Init throws, so release any subscription established here.
            _deviceLifetime?.Dispose();
            _deviceLifetime = null;
            _maximumTextureSize = 0;
            throw;
        }
    }

    protected override void OnDestroy(WebGpuCanvasContext context)
    {
        _deviceLifetime?.Dispose();
        _deviceLifetime = null;
        ReleaseSurfaces();
        _maximumTextureSize = 0;
    }

    protected override void OnWebGpuUnavailable()
        => _fallback("the active compositor does not expose a WebGPU target");

    internal void OnDeviceLost(string reason)
        => _fallback($"the WebGPU device is unavailable ({reason})");

    protected override void RenderOverride(WebGpuCanvasContext context)
    {
        if (Renderer is not { } renderer)
            return;
        if (context.PixelWidth != _pixels.Width || context.PixelHeight != _pixels.Height)
            throw new InvalidOperationException("The WebGPU target does not match the canvas's device-pixel size.");

        // Public SVG callbacks can synchronously unload this element. The CPU
        // owner cancels the frame then, but defers native pixel destruction until
        // all of the painter's canvas restore scopes have completed.
        using var paint = _raster.BeginPaint(context.PixelWidth, context.PixelHeight);
        if (_compositeLayers is { } layers)
        {
            if (!RenderComposite(paint, layers, renderer))
                return;
        }
        else if (!paint.TryRender((Owner: this, Renderer: renderer),
            static (state, canvas) => state.Owner.RenderRenderer(canvas, state.Renderer)))
        {
            return;
        }
        if (!ReferenceEquals(Renderer, renderer) || !paint.TryUpdateUpload())
            return;

        var buffer = paint.Buffer;

        var destination = new ImageCopyTexture { Texture = context.Target, Aspect = TextureAspect.All };
        var layout = new TextureDataLayout
        {
            BytesPerRow = checked((uint)buffer.UploadRowBytes),
            RowsPerImage = checked((uint)context.PixelHeight)
        };
        var extent = new Extent3D
        {
            Width = checked((uint)context.PixelWidth),
            Height = checked((uint)context.PixelHeight),
            DepthOrArrayLayers = 1
        };

        // QueueWriteTexture copies the source before returning. No managed pixel
        // array, managed swizzle, CPU mip pyramid or replacement GPU texture is needed.
        // This quad displays at one texel per device pixel; document zoom is
        // rerasterized by the original Skia painter before each upload.
        context.Api.QueueWriteTexture(context.Queue, &destination, buffer.UploadPixels.ToPointer(),
            checked((nuint)((long)buffer.UploadRowBytes * context.PixelHeight)), &layout, &extent);
        WebGpuSkiaCanvasBridge.ReportInitialization();
        if (_compositeLayers is not null)
            WebGpuSkiaCanvasBridge.ReportCompositeInitialization();
    }

    private bool RenderComposite(SkiaCanvasRasterState.Paint paint, ISkiaCanvasLayers layers,
        ISkiaCanvasRenderer renderer)
    {
        var completed = SkiaCanvasCompositePainter.Render(paint, layers, _pixels,
            (Owner: this, Layers: layers, Renderer: renderer),
            static state => ReferenceEquals(state.Owner._compositeLayers, state.Layers)
                && ReferenceEquals(state.Owner.Renderer, state.Renderer), ref _documentDirty, out var reused);
        if (completed && reused && !_reportedCompositeReuse)
        {
            _reportedCompositeReuse = true;
            Console.WriteLine("UnoDraw canvas bridge reused cached document pixels.");
        }

        return completed;
    }

    private void RenderRenderer(SKCanvas canvas, ISkiaCanvasRenderer renderer)
    {
        canvas.Clear(SKColors.Transparent);
        canvas.Save();
        try
        {
            ConfigurePainter(canvas);
            renderer.Render(canvas, _pixels.LogicalSize);
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void ConfigurePainter(SKCanvas canvas)
    {
        canvas.Scale((float)_pixels.Scale);
        canvas.ClipRect(new SKRect(0, 0, (float)_pixels.LogicalSize.Width, (float)_pixels.LogicalSize.Height));
    }

    private void ReleaseDocumentCache()
    {
        _raster.ReleaseDocumentCache();
        _documentDirty = true;
    }

    public void ReleaseSurfaces()
    {
        _documentDirty = true;
        _raster.ReleaseSurfaces();
    }
}
