// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Rectangle : IElement
    {
        public SvgRectangle svgRectangle;
        public float x;
        public float y;
        public float width;
        public float height;
        public float rx;
        public float ry;
        public bool isRound;
        public SKRect bounds;
        public SKMatrix matrix;

        public Rectangle(SvgRectangle rectangle)
        {
            svgRectangle = rectangle;
            x = svgRectangle.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            y = svgRectangle.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            width = svgRectangle.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            height = svgRectangle.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            rx = svgRectangle.CornerRadiusX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            ry = svgRectangle.CornerRadiusY.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            isRound = rx > 0f && ry > 0f;
            bounds = SKRect.Create(x, y, width, height);
            matrix = SKSvgHelper.GetSKMatrix(svgRectangle.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            var rectangle = new Rectangle(svgRectangle);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgRectangle,_disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgRectangle, disposable);
            SKSvgHelper.SetTransform(skCanvas, rectangle.matrix);

            if (svgRectangle.Fill != null)
            {
                var skPaintFill = SKSvgHelper.GetFillSKPaint(svgRectangle, skSize, rectangle.bounds, disposable);
                if (rectangle.isRound)
                {
                    skCanvas.DrawRoundRect(rectangle.x, rectangle.y, rectangle.width, rectangle.height, rectangle.rx, rectangle.ry, skPaintFill);
                }
                else
                {
                    skCanvas.DrawRect(rectangle.x, rectangle.y, rectangle.width, rectangle.height, skPaintFill);
                }
            }

            if (svgRectangle.Stroke != null)
            {
                var skPaintStroke = SKSvgHelper.GetStrokeSKPaint(svgRectangle, skSize, rectangle.bounds, disposable);
                if (rectangle.isRound)
                {
                    skCanvas.DrawRoundRect(rectangle.bounds, rectangle.rx, rectangle.ry, skPaintStroke);
                }
                else
                {
                    skCanvas.DrawRect(rectangle.bounds, skPaintStroke);
                }
            }

            if (skPaintFilter != null)
            {
                skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }
    }
}
