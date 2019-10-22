// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Fragment
    {
        public float x;
        public float y;
        public float width;
        public float height;
        public SKRect bounds;
        public SKMatrix matrix;

        public Fragment(SvgFragment svgFragment)
        {
            x = svgFragment.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgFragment);
            y = svgFragment.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgFragment);
            width = svgFragment.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgFragment);
            height = svgFragment.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgFragment);
            bounds = SKRect.Create(x, y, width, height);

            matrix = SvgHelper.GetSKMatrix(svgFragment.Transforms);
            var viewBoxMatrix = SvgHelper.GetSvgViewBoxTransform(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, width, height);
            SKMatrix.Concat(ref matrix, ref matrix, ref viewBoxMatrix);
        }
    }
}
