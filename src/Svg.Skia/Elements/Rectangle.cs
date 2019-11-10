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
            matrix = SkiaUtil.GetSKMatrix(svgRectangle.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            var rectangle = new Rectangle(svgRectangle);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgRectangle, disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgRectangle, disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            if (svgRectangle.Fill != null)
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgRectangle, skSize, bounds, disposable);
                if (isRound)
                {
                    skCanvas.DrawRoundRect(x, y, width, height, rx, ry, skPaintFill);
                }
                else
                {
                    skCanvas.DrawRect(x, y, width, height, skPaintFill);
                }
            }

            if (svgRectangle.Stroke != null)
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgRectangle, skSize, bounds, disposable);
                if (isRound)
                {
                    skCanvas.DrawRoundRect(bounds, rx, ry, skPaintStroke);
                }
                else
                {
                    skCanvas.DrawRect(bounds, skPaintStroke);
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
