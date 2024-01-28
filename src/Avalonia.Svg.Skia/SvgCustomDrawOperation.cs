using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Svg.Skia;

namespace Avalonia.Svg.Skia;

public class SvgCustomDrawOperation : ICustomDrawOperation
{
    private readonly SKSvg? _svg;

    public SvgCustomDrawOperation(Rect bounds, SKSvg? svg)
    {
        _svg = svg;
        Bounds = bounds;
    }

    public void Dispose()
    {
    }

    public Rect Bounds { get; }

    public bool HitTest(Point p) => false;

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(IDrawingContextImpl context)
    {
        if (_svg?.Picture is null)
        {
            return;
        }
        
var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
        if (canvas is null)
        {
            return;
        }

        canvas.Save();
        canvas.DrawPicture(_svg.Picture);
        canvas.Restore();
    }
}
