// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Polyline : IElement
    {
        public SvgPolyline svgPolyline;
        public SKMatrix matrix;

        public Polyline(SvgPolyline polyline)
        {
            svgPolyline = polyline;
            matrix = SkiaUtil.GetSKMatrix(svgPolyline.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgPolyline, disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgPolyline, disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            var skPath = SkiaUtil.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false, disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (svgPolyline.Fill != null)
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPolyline, skSize, skBounds, disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (svgPolyline.Stroke != null)
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolyline, skSize, skBounds, disposable);
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
