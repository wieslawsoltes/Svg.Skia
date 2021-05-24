using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Avalonia.SKPictureImage
{
    public class SKBitmapDrawOperation : ICustomDrawOperation
    {
        private readonly SKBitmap? _bitmap;
        private readonly Rect _bounds;

        public SKBitmapDrawOperation(Rect bounds, SKBitmap? bitmap)
        {
            _bitmap = bitmap;
            _bounds = bounds;
        }

        public void Dispose()
        {
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(IDrawingContextImpl context)
        {
            var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
            if (canvas is { } && _bitmap is { })
            {
                canvas.DrawBitmap(
                    _bitmap,
                    SKRect.Create(0, 0, _bitmap.Width, _bitmap.Height),
                    SKRect.Create((float)_bounds.Left, (float)_bounds.Top, (float)_bounds.Width, (float)_bounds.Height),
                    null);
            }
        }
    }
}
