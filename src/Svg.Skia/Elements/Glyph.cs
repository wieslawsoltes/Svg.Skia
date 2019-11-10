// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Glyph : IElement
    {
        public SvgGlyph svgGlyph;
        public SKMatrix matrix;

        public Glyph(SvgGlyph glyph)
        {
            svgGlyph = glyph;
            matrix = SKSvgHelper.GetSKMatrix(svgGlyph.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgGlyph, disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgGlyph, disposable);
            SKSvgHelper.SetTransform(skCanvas, matrix);

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
