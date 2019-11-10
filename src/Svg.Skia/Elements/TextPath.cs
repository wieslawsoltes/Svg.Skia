// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct TextPath : IElement
    {
        public SvgTextPath svgTextPath;
        public SKMatrix matrix;

        public TextPath(SvgTextPath textPath)
        {
            svgTextPath = textPath;
            matrix = SkiaUtil.GetSKMatrix(svgTextPath.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgTextPath, disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgTextPath, disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            // TODO:

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
