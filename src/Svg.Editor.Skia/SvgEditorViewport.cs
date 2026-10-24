using System;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// A device-independent, cursor-anchored viewport. Camera changes never mutate the SVG document.
/// </summary>
public sealed class SvgEditorViewport
{
    public const double MinimumZoom = 0.01;
    public const double MaximumZoom = 128;

    public event EventHandler? Changed;

    public double Zoom { get; private set; } = 1;
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    public double Width { get; private set; }
    public double Height { get; private set; }

    public SKMatrix Matrix => new((float)Zoom, 0, (float)OffsetX, 0, (float)Zoom, (float)OffsetY, 0, 0, 1);

    public SKRect VisibleWorldBounds => new(
        (float)(-OffsetX / Zoom),
        (float)(-OffsetY / Zoom),
        (float)((Width - OffsetX) / Zoom),
        (float)((Height - OffsetY) / Zoom));

    public SKPoint WorldToView(SKPoint point) => new(
        (float)(point.X * Zoom + OffsetX),
        (float)(point.Y * Zoom + OffsetY));

    public SKPoint ViewToWorld(SKPoint point) => new(
        (float)((point.X - OffsetX) / Zoom),
        (float)((point.Y - OffsetY) / Zoom));

    public void Resize(double width, double height)
    {
        if (!IsFiniteFloat(width) || !IsFiniteFloat(height) || width < 0 || height < 0)
        {
            return;
        }

        if (Width == width && Height == height)
        {
            return;
        }

        var x = OffsetX;
        var y = OffsetY;
        if (Width > 0 && Height > 0)
        {
            x += (width - Width) / 2;
            y += (height - Height) / 2;
        }

        if (!IsValidCamera(Zoom, x, y, width, height))
        {
            return;
        }

        OffsetX = x;
        OffsetY = y;
        Width = width;
        Height = height;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Pan(double deltaX, double deltaY)
    {
        var x = OffsetX + deltaX;
        var y = OffsetY + deltaY;
        if (!IsValidCamera(Zoom, x, y, Width, Height) || (x == OffsetX && y == OffsetY))
        {
            return;
        }

        OffsetX = x;
        OffsetY = y;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomAt(double factor, SKPoint anchor)
    {
        if (!IsFinite(factor) || factor <= 0)
        {
            return;
        }

        SetZoomAt(Zoom * factor, anchor);
    }

    public void SetZoomAt(double zoom, SKPoint anchor)
    {
        if (!IsFinite(zoom) || !IsFinite(anchor.X) || !IsFinite(anchor.Y) || zoom <= 0)
        {
            return;
        }

        zoom = Math.Max(MinimumZoom, Math.Min(MaximumZoom, zoom));
        if (zoom == Zoom)
        {
            return;
        }

        // Compute in double precision: repeated reciprocal zooms must not accumulate camera drift.
        var worldX = (anchor.X - OffsetX) / Zoom;
        var worldY = (anchor.Y - OffsetY) / Zoom;
        var x = anchor.X - worldX * zoom;
        var y = anchor.Y - worldY * zoom;
        if (!IsValidCamera(zoom, x, y, Width, Height))
        {
            return;
        }

        OffsetX = x;
        OffsetY = y;
        Zoom = zoom;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Applies a scale and moving anchor atomically, keeping the world point under
    /// the previous midpoint beneath the new midpoint, including at zoom limits.
    /// </summary>
    public void PanAndZoomAt(double factor, SKPoint previousAnchor, SKPoint currentAnchor)
    {
        var zoom = Zoom * factor;
        if (!IsFinite(zoom) || zoom <= 0 || !IsFinite(previousAnchor.X) || !IsFinite(previousAnchor.Y)
            || !IsFinite(currentAnchor.X) || !IsFinite(currentAnchor.Y))
            return;
        zoom = Math.Max(MinimumZoom, Math.Min(MaximumZoom, zoom));
        var x = currentAnchor.X - ((previousAnchor.X - OffsetX) / Zoom) * zoom;
        var y = currentAnchor.Y - ((previousAnchor.Y - OffsetY) / Zoom) * zoom;
        if (!IsValidCamera(zoom, x, y, Width, Height)
            || (zoom == Zoom && x == OffsetX && y == OffsetY))
            return;
        Zoom = zoom;
        OffsetX = x;
        OffsetY = y;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Fit(SKRect bounds, double padding = 48, double maximumZoom = 4)
    {
        if (Width <= 0 || Height <= 0 || !IsFinite(bounds.Left) || !IsFinite(bounds.Top)
            || !IsFinite(bounds.Right) || !IsFinite(bounds.Bottom)
            || !IsFinite(bounds.Width) || !IsFinite(bounds.Height)
            || bounds.Width <= 0 || bounds.Height <= 0 || !IsFinite(padding)
            || !IsFinite(maximumZoom) || maximumZoom <= 0)
        {
            return;
        }

        padding = Math.Max(0, Math.Min(padding, Math.Min(Width, Height) * 0.4));
        var zoom = Math.Min((Width - padding * 2) / bounds.Width, (Height - padding * 2) / bounds.Height);
        zoom = Math.Max(MinimumZoom, Math.Min(Math.Min(MaximumZoom, maximumZoom), zoom));
        var x = Width / 2 - ((double)bounds.Left + bounds.Right) / 2 * zoom;
        var y = Height / 2 - ((double)bounds.Top + bounds.Bottom) / 2 * zoom;
        if (!IsValidCamera(zoom, x, y, Width, Height))
        {
            return;
        }

        Zoom = zoom;
        OffsetX = x;
        OffsetY = y;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ResetZoom() => SetZoomAt(1, new SKPoint((float)(Width / 2), (float)(Height / 2)));

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool IsFiniteFloat(double value) => IsFinite(value) && Math.Abs(value) <= float.MaxValue;

    private static bool IsValidCamera(double zoom, double x, double y, double width, double height) =>
        IsFiniteFloat(x) && IsFiniteFloat(y)
        && IsFiniteFloat(-x / zoom) && IsFiniteFloat(-y / zoom)
        && IsFiniteFloat((width - x) / zoom) && IsFiniteFloat((height - y) / zoom);
}
