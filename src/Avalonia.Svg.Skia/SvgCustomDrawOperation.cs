using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

namespace Avalonia.Svg.Skia
{
    internal class SvgCustomDrawOperation : ICustomDrawOperation
    {
        private readonly SvgSource _svg;

        public SvgCustomDrawOperation(Rect bounds, SvgSource svg)
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
            if (_svg == null || _svg.Picture == null)
            {
                return;
            }

            var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
            if (canvas == null)
            {
                return;
            }

            canvas.Save();
            canvas.DrawPicture(_svg.Picture);
            canvas.Restore();
        }
    }
}
