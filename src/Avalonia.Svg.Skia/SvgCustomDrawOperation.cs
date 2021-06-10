using System;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Svg.Skia;

namespace Avalonia.Svg.Skia
{
    public class SvgCustomDrawOperation : ICustomDrawOperation
    {
        private readonly SKSvg? _svg;
        private readonly double _opacity;

        public SvgCustomDrawOperation(Rect bounds, SKSvg? svg, double opacity)
        {
            _svg = svg;
            _opacity = opacity;
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

            // HACK: Opacity
            bool enableOpacity = _opacity < 1.0f;
            if (enableOpacity)
            {
                var opacityPaint = new SkiaSharp.SKPaint
                {
                    IsAntialias = true,
                    Color = new SkiaSharp.SKColor(255, 255, 255, (byte)Math.Round(_opacity * 255)),
                    Style = SkiaSharp.SKPaintStyle.StrokeAndFill
                };
                canvas.SaveLayer(opacityPaint);
            }

            canvas.Save();
            canvas.DrawPicture(_svg.Picture);
            // NOTE: DEBUG
            // canvas.DrawRect(
            //     Bounds.ToSKRect(), 
            //     new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 5});
            canvas.Restore();

            // HACK: Opacity
            if (enableOpacity)
            {
                canvas.Restore();
            }
        }
    }
}
