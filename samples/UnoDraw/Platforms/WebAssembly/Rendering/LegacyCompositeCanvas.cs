using Microsoft.UI.Xaml;
using SkiaSharp;
using Svg.Editor.Skia.Uno;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace UnoDraw.Rendering;

/// <summary>
/// Combines cached document pixels and current feedback inside Uno's original
/// software upload. The host retains texture creation, opacity and mip handling.
/// </summary>
// Deliberately not ISkiaCanvasRenderer: the island factory declines this child
// once, through IGLIslandRenderer.OnUnavailable, enabling PaintSoftware without
// constructing another canvas. SKCanvasElement remembers that fallback attempt.
internal sealed class LegacyCompositeCanvas(Action<string?> fallback) : SKCanvasElement, ISkiaCanvasIslandTarget
{
    private readonly SkiaCanvasRasterState _raster = new();
    private ISkiaCanvasLayers? _layers;
    private CanvasPixelSize _configured;
    private (CanvasPixelSize Pixels, SKImageInfo Info, SKRectI Clip)? _geometry;
    private WeakReference<SKSurface>? _borrowedOwner;
    private bool _documentDirty = true;
    private bool _reportedReuse;
    private bool _failed;

    public FrameworkElement Element => this;
    public ISkiaCanvasRenderer? Renderer { get; set; }

    public bool Configure(CanvasPixelSize pixels)
    {
        if (_failed)
            return false;
        if (_configured == pixels)
            return true;

        _configured = pixels;
        ReleaseSurfaces();
        // Uno's original uploader applies the device scale. This child stays in
        // the parent's already arranged logical coordinates, with no DPR transform.
        UseLayoutRounding = false;
        IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Width = pixels.LogicalSize.Width;
        Height = pixels.LogicalSize.Height;
        Invalidate();
        return true;
    }

    public void SetCompositeLayers(ISkiaCanvasLayers? layers)
    {
        if (ReferenceEquals(_layers, layers))
            return;
        _layers = layers;
        ReleaseSurfaces();
        Invalidate();
    }

    public void InvalidateDocument()
    {
        _documentDirty = true;
        Invalidate();
    }

    public void InvalidateInteraction() => Invalidate();

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_failed || Renderer is not { } renderer || _layers is not { } layers)
            return;

        try
        {
            // Only this non-renderer child uses the deliberate software fallback.
            // Do not infer ownership from an arbitrary CPU canvas or dispose the
            // surface wrapper returned by its borrowed SKCanvas.
            var surface = canvas.Surface;
            var matrix = canvas.TotalMatrix;
            if (surface is null || canvas.SaveCount != 2 || !canvas.IsClipRect
                || !float.IsFinite(matrix.ScaleX) || matrix.ScaleX <= 0
                || matrix != SKMatrix.CreateScale(matrix.ScaleX, matrix.ScaleX))
            {
                RequestFallback("the original software canvas does not expose the expected private raster surface");
                return;
            }

            SKImageInfo info;
            using (var probe = surface.PeekPixels())
            {
                if (probe is null || probe.ColorType != SKColorType.Bgra8888 || probe.AlphaType != SKAlphaType.Premul
                    || probe.ColorSpace is not null || probe.Width <= 0 || probe.Height <= 0
                    || (long)probe.Width * probe.Height > int.MaxValue / 4)
                {
                    RequestFallback("the original software canvas has no compatible BGRA pixels");
                    return;
                }
                info = probe.Info;
            }

            // PaintSoftware computes dimensions from its float device matrix.
            // Its actual target is authoritative, including fractional trailing
            // pixels; a second double-to-float size calculation could differ.
            var pixels = new CanvasPixelSize(area, matrix.ScaleX, info.Width, info.Height);
            var geometry = (pixels, info, canvas.DeviceClipBounds);
            if (_geometry != geometry || _borrowedOwner?.TryGetTarget(out var previous) != true
                || !ReferenceEquals(previous, surface))
            {
                _raster.ReleaseDocumentCache();
                _documentDirty = true;
                _geometry = geometry;
                _borrowedOwner = new WeakReference<SKSurface>(surface);
            }

            using var paint = _raster.BeginPaint(info.Width, info.Height);
            if (!SkiaCanvasCompositePainter.Render(paint, layers, pixels,
                (Owner: this, Layers: layers, Renderer: renderer),
                static state => !state.Owner._failed && ReferenceEquals(state.Owner._layers, state.Layers)
                    && ReferenceEquals(state.Owner.Renderer, state.Renderer), ref _documentDirty, out var reused))
                return;

            // SVG callbacks receive only the owned raster, and can unload the
            // island. Reacquire the host view after all callbacks and check the
            // paint generation before touching its native memory.
            if (!paint.IsCurrent || surface.Handle == IntPtr.Zero || canvas.Handle == IntPtr.Zero)
                return;
            using var destination = surface.PeekPixels();
            if (destination is null)
            {
                RequestFallback("the original software canvas pixels became unavailable");
                return;
            }
            // PaintSoftware has already cleared this private target, notifying
            // Skia and detaching any previous snapshots. No host drawing or
            // snapshot callback occurs between that clear and this complete copy.
            if (!paint.TryCopyRasterTo(destination))
                return;
            GC.KeepAlive(surface);
            GC.KeepAlive(canvas);
            WebGpuSkiaCanvasBridge.ReportLegacyCompositeInitialization();
            if (reused && !_reportedReuse)
            {
                _reportedReuse = true;
                Console.WriteLine("UnoDraw legacy-composite canvas bridge reused cached document pixels.");
            }
        }
        catch (Exception error)
        {
            RequestFallback(error.Message);
        }
    }

    private void RequestFallback(string reason)
    {
        if (_failed)
            return;
        _failed = true;
        ReleaseSurfaces();
        fallback(reason);
    }

    public void ReleaseSurfaces()
    {
        _geometry = null;
        _borrowedOwner = null;
        _documentDirty = true;
        _raster.ReleaseSurfaces();
    }
}
