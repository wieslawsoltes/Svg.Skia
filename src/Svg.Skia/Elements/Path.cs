// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Path : IElement
    {
        public SvgPath svgPath;
        public SKMatrix matrix;

        public Path(SvgPath path)
        {
            svgPath = path;
            matrix = SkiaUtil.GetSKMatrix(svgPath.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgPath, disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgPath, disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            var skPath = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (svgPath.Fill != null)
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPath, skSize, skBounds, disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (svgPath.Stroke != null)
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgPath, skSize, skBounds, disposable);
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
