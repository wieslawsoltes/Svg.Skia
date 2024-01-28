using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Avalonia.Controls.Skia;

public class SKPictureDrawOperation : ICustomDrawOperation
{
    private readonly SKPicture? _picture;
    private readonly Rect _bounds;

    public SKPictureDrawOperation(Rect bounds, SKPicture? picture)
    {
        _picture = picture;
        _bounds = bounds;
    }

    public void Dispose()
    {
    }

    public Rect Bounds => _bounds;

    public bool HitTest(Point p) => _bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(IDrawingContextImpl context)
    {
var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
        if (canvas is null || _picture is null)
        {
            return;
        }

        canvas.Save();
        canvas.DrawPicture(_picture);
        canvas.Restore();
    }
}
