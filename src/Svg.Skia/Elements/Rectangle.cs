// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Rectangle
    {
        public float x;
        public float y;
        public float width;
        public float height;
        public float rx;
        public float ry;
        public bool isRound;
        public SKRect bounds;
        public SKMatrix matrix;

        public Rectangle(SvgRectangle svgRectangle)
        {
            x = svgRectangle.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            y = svgRectangle.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            width = svgRectangle.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            height = svgRectangle.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            rx = svgRectangle.CornerRadiusX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            ry = svgRectangle.CornerRadiusY.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            isRound = rx > 0f && ry > 0f;
            bounds = SKRect.Create(x, y, width, height);
            matrix = SvgHelper.GetSKMatrix(svgRectangle.Transforms);
        }
    }
}
