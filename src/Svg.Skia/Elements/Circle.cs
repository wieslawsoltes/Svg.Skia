// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Circle : IElement
    {
        public SvgCircle svgCircle;
        public float cx;
        public float cy;
        public float radius;
        public SKRect bounds;
        public SKMatrix matrix;

        public Circle(SvgCircle circle)
        {
            svgCircle = circle;
            cx = svgCircle.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgCircle);
            cy = svgCircle.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgCircle);
            radius = svgCircle.Radius.ToDeviceValue(null, UnitRenderingType.Other, svgCircle);
            bounds = SKRect.Create(cx - radius, cy - radius, radius + radius, radius + radius);
            matrix = SKSvgHelper.GetSKMatrix(svgCircle.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            var circle = new Circle(svgCircle);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgCircle, disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgCircle, disposable);
            SKSvgHelper.SetTransform(skCanvas, matrix);

            if (svgCircle.Fill != null)
            {
                var skPaintFill = SKSvgHelper.GetFillSKPaint(svgCircle, skSize, bounds, disposable);
                skCanvas.DrawCircle(cx, cy, radius, skPaintFill);
            }

            if (svgCircle.Stroke != null)
            {
                var skPaintStroke = SKSvgHelper.GetStrokeSKPaint(svgCircle, skSize, bounds, disposable);
                skCanvas.DrawCircle(cx, cy, radius, skPaintStroke);
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
