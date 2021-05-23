using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Avalonia.SKPictureImage
{
    public class SKPictureDrawOperation : ICustomDrawOperation
    {
        private readonly SKPicture? _picture;

        public SKPictureDrawOperation(Rect bounds, SKPicture? picture)
        {
            _picture = picture;
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
            var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
            if (canvas is { } && _picture is { })
            {
                canvas.DrawPicture(_picture);
            }
        }
    }
}
