using Microsoft.UI.Xaml;
using SkiaSharp;
using Svg.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

/// <summary>Owns an optional Svg.Skia renderer and replays its picture directly on Uno's canvas.</summary>
public sealed class StudioSvgPreview : SKCanvasElement, IDisposable
{
    private SKSvg? _renderer;
    private bool _disposed;
    public void SetRenderer(SKSvg? renderer)
    {
        if (_disposed) { renderer?.Dispose(); return; }
        if (ReferenceEquals(_renderer, renderer)) return;
        var previous = _renderer; _renderer = renderer; previous?.Dispose(); Invalidate();
    }
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_disposed) return;
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
        using var paint = new SKPaint { Color = ActualTheme == ElementTheme.Dark ? new SKColor(40, 44, 51) : new SKColor(233, 235, 240) };
        canvas.DrawRect(SKRect.Create((float)area.Width, (float)area.Height), paint);
        paint.Color = ActualTheme == ElementTheme.Dark ? new SKColor(57, 62, 71) : new SKColor(210, 215, 224);
        for (var y = 0; y < area.Height; y += 12)
            for (var x = 0; x < area.Width; x += 12)
                if (((x / 12 + y / 12) & 1) == 0) canvas.DrawRect(x, y, 12, 12, paint);
        if (_renderer?.Picture is not { } picture || area.Width <= 16 || area.Height <= 16) return;
        var bounds = picture.CullRect;
        if (!float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height)) return;
        var scale = Math.Min((area.Width - 16) / Math.Max(1, bounds.Width), (area.Height - 16) / Math.Max(1, bounds.Height));
        canvas.Translate((float)area.Width / 2, (float)area.Height / 2);
        canvas.Scale((float)scale); canvas.Translate(-bounds.MidX, -bounds.MidY);
        _renderer.Draw(canvas);
    }
    public new void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _renderer?.Dispose(); _renderer = null; base.Dispose();
    }
}
