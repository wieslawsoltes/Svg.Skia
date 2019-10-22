// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Circle
    {
        public float cx;
        public float cy;
        public float radius;
        public SKRect bounds;
        public SKMatrix matrix;

        public Circle(SvgCircle svgCircle)
        {
            cx = svgCircle.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgCircle);
            cy = svgCircle.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgCircle);
            radius = svgCircle.Radius.ToDeviceValue(null, UnitRenderingType.Other, svgCircle);
            bounds = SKRect.Create(cx - radius, cy - radius, radius + radius, radius + radius);
            matrix = SvgHelper.GetSKMatrix(svgCircle.Transforms);
        }
    }
}
