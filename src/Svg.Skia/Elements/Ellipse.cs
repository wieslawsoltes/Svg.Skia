// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Ellipse : IElement
    {
        public SvgEllipse svgEllipse;
        public float cx;
        public float cy;
        public float rx;
        public float ry;
        public SKRect bounds;
        public SKMatrix matrix;

        public Ellipse(SvgEllipse ellipse)
        {
            svgEllipse = ellipse;
            cx = svgEllipse.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgEllipse);
            cy = svgEllipse.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgEllipse);
            rx = svgEllipse.RadiusX.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);
            ry = svgEllipse.RadiusY.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);
            bounds = SKRect.Create(cx - rx, cy - ry, rx + rx, ry + ry);
            matrix = SKSvgHelper.GetSKMatrix(svgEllipse.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            var ellipse = new Ellipse(svgEllipse);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgEllipse, disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgEllipse, disposable);
            SKSvgHelper.SetTransform(skCanvas, ellipse.matrix);

            if (svgEllipse.Fill != null)
            {
                var skPaintFill = SKSvgHelper.GetFillSKPaint(svgEllipse, skSize, ellipse.bounds, disposable);
                skCanvas.DrawOval(ellipse.cx, ellipse.cy, ellipse.rx, ellipse.ry, skPaintFill);
            }

            if (svgEllipse.Stroke != null)
            {
                var skPaintStroke = SKSvgHelper.GetStrokeSKPaint(svgEllipse, skSize, ellipse.bounds, disposable);
                skCanvas.DrawOval(ellipse.cx, ellipse.cy, ellipse.rx, ellipse.ry, skPaintStroke);
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
