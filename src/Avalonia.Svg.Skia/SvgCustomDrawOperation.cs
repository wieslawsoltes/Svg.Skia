using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Svg.Skia;

namespace Avalonia.Svg.Skia;

public class SvgCustomDrawOperation(Rect bounds, SKSvg? svg) : ICustomDrawOperation
{
    public void Dispose()
    {
    }

    public Rect Bounds { get; } = bounds;

    public bool HitTest(Point p) => false;

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        if (svg?.Picture is null)
        {
            return;
        }
        
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null)
        {
            return;
        }
        using var lease = leaseFeature.Lease();
        var canvas = lease?.SkCanvas;
        if (canvas is null)
        {
            return;
        }
        lock (_svg.Locker)
        {
            var picture = _svg.Picture;
            if (picture is null)
                return;
            canvas.Save();
            canvas.DrawPicture(picture);
            canvas.Restore();
        }
    }
}
