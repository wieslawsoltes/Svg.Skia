// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using ShimSkiaSharp;
using Svg.FilterEffects;

namespace Svg.Model;

internal class SvgFilterPrimitiveContext
{
    public SvgFilterPrimitiveContext(SvgFilterPrimitive svgFilterPrimitive)
    {
        FilterPrimitive = svgFilterPrimitive;
    }

    public SvgFilterPrimitive FilterPrimitive { get; }

    public SKRect Boundaries { get; set; }

    public bool IsXValid { get; set; }

    public bool IsYValid { get; set; }

    public bool IsWidthValid { get; set; }

    public bool IsHeightValid { get; set; }

    public SvgUnit X { get; set; }

    public SvgUnit Y { get; set; }

    public SvgUnit Width { get; set; }

    public SvgUnit Height { get; set; }
}
