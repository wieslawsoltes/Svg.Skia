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

        public Polygon(SvgPolygon polygon)
        {
            svgPolygon = polygon;
            matrix = SkiaUtil.GetSKMatrix(svgPolygon.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgPolygon, disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgPolygon, disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            var skPath = SkiaUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (svgPolygon.Fill != null)
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPolygon, skSize, skBounds, disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (svgPolygon.Stroke != null)
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolygon, skSize, skBounds, disposable);
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
