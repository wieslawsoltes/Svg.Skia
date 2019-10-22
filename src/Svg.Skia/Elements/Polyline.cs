// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Polyline
    {
        public SKMatrix matrix;

        public Polyline(SvgPolyline svgPolyline)
        {
            matrix = SvgHelper.GetSKMatrix(svgPolyline.Transforms);
        }
    }
}
