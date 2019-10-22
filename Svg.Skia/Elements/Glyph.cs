// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Glyph
    {
        public SKMatrix matrix;

        public Glyph(SvgGlyph svgGlyph)
        {
            matrix = SvgHelper.GetSKMatrix(svgGlyph.Transforms);
        }
    }
}
