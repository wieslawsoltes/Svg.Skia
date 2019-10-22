// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Ellipse
    {
        public float cx;
        public float cy;
        public float rx;
        public float ry;
        public SKRect bounds;
        public SKMatrix matrix;

        public Ellipse(SvgEllipse svgEllipse)
        {
            cx = svgEllipse.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgEllipse);
            cy = svgEllipse.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgEllipse);
            rx = svgEllipse.RadiusX.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);
            ry = svgEllipse.RadiusY.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);
            bounds = SKRect.Create(cx - rx, cy - ry, rx + rx, ry + ry);
            matrix = SvgHelper.GetSKMatrix(svgEllipse.Transforms);
        }
    }
}
