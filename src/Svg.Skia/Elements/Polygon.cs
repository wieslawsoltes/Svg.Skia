// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Polygon : IElement
    {
        public SvgPolygon svgPolygon;
        public SKMatrix matrix;

        public Polygon(SvgPolygon svgPolygon)
        {
            svgPolygon = polygon;
            matrix = SKSvgHelper.GetSKMatrix(svgPolygon.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgPolygon, disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgPolygon, disposable);
            SKSvgHelper.SetTransform(skCanvas, matrix);

            var skPath = SKSvgHelper.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (svgPolygon.Fill != null)
                {
                    var skPaint = SKSvgHelper.GetFillSKPaint(svgPolygon, skSize, skBounds, disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (svgPolygon.Stroke != null)
                {
                    var skPaint = SKSvgHelper.GetStrokeSKPaint(svgPolygon, skSize, skBounds, disposable);
                    skCanvas.DrawPath(skPath, skPaint);
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
