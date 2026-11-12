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
internal sealed unsafe class WebGpuRasterCanvas : WebGpuCanvasElement
{
    private readonly Action<string?> _fallback;
    private readonly bool _preferWebGl;
    private readonly bool _preferGraphite;
    private readonly Action<SKCanvas> _drawGraphiteFrame;
    private readonly ScaleTransform _displayScale = new();
    private CanvasPixelSize _pixels;
    private SkiaCanvasUploadBuffer? _buffer;
    private SkiaCanvasDocumentCache? _documentCache;
    private ISkiaCanvasLayers? _compositeLayers;
    private bool _documentDirty = true;
    private bool _reportedCompositeReuse;
    private WebGlSkiaCanvas? _webGlCanvas;
    private bool _webGlUnavailable;
    private WebGpuGraphiteCanvas? _graphiteCanvas;
    private SkiaGpuDocumentPainter? _graphiteDocument;
    private string? _reportedGraphiteDocumentFailure;
    private bool _graphiteUnavailable;
    private uint _maximumTextureSize;
    private IDisposable? _deviceLifetime;

    internal WebGpuRasterCanvas(Action<string?> fallback, bool preferWebGl, bool preferGraphite)
    {
        _fallback = fallback;
        _preferWebGl = preferWebGl;
        _preferGraphite = preferGraphite;
        _drawGraphiteFrame = DrawGraphiteFrame;
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

    internal ISkiaCanvasRenderer? Renderer { get; set; }

    internal void SetCompositeLayers(ISkiaCanvasLayers? layers)
    {
        if (ReferenceEquals(_compositeLayers, layers))
            return;
        _compositeLayers = layers;
        ReleaseDocumentCache();
        Invalidate();
    }

    // Ordinary SKCanvasElement invalidation reaches the island here, including
    // calls made through its nonvirtual base API. Feedback uses Invalidate directly.
    internal void InvalidateDocument()
    {
        _documentDirty = true;
        Invalidate();
    }

    internal bool Configure(CanvasPixelSize pixels)
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

        // The measured bottleneck is the document raster. Small auxiliary and
        // retained interaction surfaces keep their existing CPU/pixel path.
        if (_preferGraphite && !_graphiteUnavailable && renderer is SvgEditorCanvas editor)
        {
            try
            {
                _graphiteDocument ??= new SkiaGpuDocumentPainter(directReplay: true);
                if (_graphiteDocument.Prepare(editor.Controller?.Renderer,
                    _pixels.Width, _pixels.Height, _pixels.Scale, _pixels.LogicalSize.Width,
                    _pixels.LogicalSize.Height, editor.Viewport.Matrix, out var reason))
                {
                    _graphiteCanvas ??= new WebGpuGraphiteCanvas(OnGraphiteUnavailable);
                    if (_graphiteCanvas.Render(context, _pixels, _drawGraphiteFrame))
                    {
                        _reportedGraphiteDocumentFailure = null;
                        return;
                    }
                }
                else
                {
                    ReportGraphiteDocumentFallback(reason!);
                }
            }
            catch (SkiaDocumentRasterizationException error)
            {
                ReportGraphiteDocumentFallback(error.Message);
            }
            catch (Exception error)
            {
                OnGraphiteUnavailable(error.Message);
            }
            if (Renderer is null)
                return; // A synchronous document callback unloaded this canvas.
        }
        if (_preferWebGl && !_webGlUnavailable && renderer is SvgEditorCanvas)
        {
            try
            {
                _webGlCanvas ??= new WebGlSkiaCanvas(OnWebGlUnavailable);
                if (_webGlCanvas.Render(context, _pixels, renderer))
                    return;
            }
            catch (Exception error)
            {
                OnWebGlUnavailable(error.Message);
                // Repaint this same target through the reliable CPU path now.
            }
            if (Renderer is null)
                return; // A synchronous document callback unloaded this canvas.
        }

        if (_buffer is null || _buffer.Width != context.PixelWidth || _buffer.Height != context.PixelHeight)
        {
            _buffer?.Dispose();
            _buffer = new SkiaCanvasUploadBuffer(context.PixelWidth, context.PixelHeight);
        }

        var buffer = _buffer;
        if (_compositeLayers is { } layers)
        {
            RenderComposite(buffer, layers);
        }
        else
        {
            var canvas = buffer.Canvas;
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
        buffer.UpdateUpload();

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

    private void DrawGraphiteFrame(SKCanvas canvas)
    {
        if (Renderer is SvgEditorCanvas editor && _graphiteDocument is { } painter)
            editor.RenderWithDocument(canvas, _pixels.LogicalSize, painter.DrawDocument);
    }

    private void ReportGraphiteDocumentFallback(string reason)
    {
        if (_reportedGraphiteDocumentFailure == reason)
            return;
        _reportedGraphiteDocumentFailure = reason;
        Console.WriteLine($"UnoDraw Graphite canvas fallback: {reason}.");
    }

    private void RenderComposite(SkiaCanvasUploadBuffer buffer, ISkiaCanvasLayers layers)
    {
        var restored = !_documentDirty && layers.IsDocumentCurrent(_pixels.LogicalSize)
            && _documentCache?.TryRestore(buffer) == true;
        if (!restored)
        {
            // Consume before painting so a full invalidation raised by a painter
            // remains pending. Only the completed document is retained; feedback
            // never becomes part of the backdrop copied on the next interaction.
            _documentDirty = false;
            buffer.Canvas.Clear(SKColors.Transparent);
            RenderLayer(buffer.Canvas, layers, document: true);
            (_documentCache ??= new SkiaCanvasDocumentCache()).Store(buffer);
        }
        else if (!_reportedCompositeReuse)
        {
            _reportedCompositeReuse = true;
            Console.WriteLine("UnoDraw canvas bridge reused cached document pixels.");
        }

        RenderLayer(buffer.Canvas, layers, document: false);
    }

    private void RenderLayer(SKCanvas canvas, ISkiaCanvasLayers layers, bool document)
    {
        canvas.Save();
        try
        {
            ConfigurePainter(canvas);
            if (document)
                layers.RenderDocument(canvas, _pixels.LogicalSize);
            else
                layers.RenderInteraction(canvas, _pixels.LogicalSize);
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
        _documentCache?.Dispose();
        _documentCache = null;
        _documentDirty = true;
    }

    private void OnWebGlUnavailable(string reason)
    {
        if (_webGlUnavailable)
            return;
        _webGlUnavailable = true;
        Console.WriteLine($"UnoDraw WebGL canvas fallback: {reason}.");
        var canvas = _webGlCanvas;
        _webGlCanvas = null;
        try
        {
            canvas?.Dispose();
        }
        catch (Exception error)
        {
            // A lost browser context may also reject its teardown. It must not
            // prevent the same-frame CPU repaint or an asynchronous fallback.
            Console.WriteLine($"UnoDraw WebGL canvas fallback cleanup: {error.Message}.");
        }
        finally
        {
            Invalidate();
        }
    }

    private void OnGraphiteUnavailable(string reason)
    {
        if (_graphiteUnavailable)
            return;
        _graphiteUnavailable = true;
        Console.WriteLine($"UnoDraw Graphite canvas fallback: {reason}.");
        var canvas = _graphiteCanvas;
        _graphiteCanvas = null;
        _graphiteDocument?.Dispose();
        _graphiteDocument = null;
        try
        {
            canvas?.Dispose();
        }
        catch (Exception error)
        {
            Console.WriteLine($"UnoDraw Graphite canvas fallback cleanup: {error.Message}.");
        }
        finally
        {
            Invalidate();
        }
    }

    internal void ReleaseSurfaces()
    {
        var graphite = _graphiteCanvas;
        _graphiteCanvas = null;
        _graphiteDocument?.Dispose();
        _graphiteDocument = null;
        var webGl = _webGlCanvas;
        _webGlCanvas = null;
        try
        {
            try
            {
                graphite?.Dispose();
            }
            finally
            {
                webGl?.Dispose();
            }
        }
        finally
        {
            ReleaseDocumentCache();
            _buffer?.Dispose();
            _buffer = null;
        }
    }
}
