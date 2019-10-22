// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Line
    {
        public float x0;
        public float y0;
        public float x1;
        public float y1;
        public SKRect bounds;
        public SKMatrix matrix;

        public Line(SvgLine svgLine)
        {
            x0 = svgLine.StartX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgLine);
            y0 = svgLine.StartY.ToDeviceValue(null, UnitRenderingType.Vertical, svgLine);
            x1 = svgLine.EndX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgLine);
            y1 = svgLine.EndY.ToDeviceValue(null, UnitRenderingType.Vertical, svgLine);
            float x = Math.Min(x0, x1);
            float y = Math.Min(y0, y1);
            float width = Math.Abs(x0 - x1);
            float height = Math.Abs(y0 - y1);
            bounds = SKRect.Create(x, y, width, height);
            matrix = SvgHelper.GetSKMatrix(svgLine.Transforms);
        }
    }
}
