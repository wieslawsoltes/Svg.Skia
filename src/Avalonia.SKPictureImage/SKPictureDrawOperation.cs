using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

namespace Avalonia.SKPictureImage
{
    public class SKPictureDrawOperation : ICustomDrawOperation
    {
        private readonly SkiaSharp.SKPicture? _picture;

        public SKPictureDrawOperation(Rect bounds, SkiaSharp.SKPicture? picture)
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
            if (canvas is null || _picture is null)
            {
                return;
            }

            canvas.Save();
            canvas.DrawPicture(_picture);
            canvas.Restore();
        }
    }
}
