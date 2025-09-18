// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using ShimSkiaSharp;
using Svg.DataTypes;

namespace Svg.Model;

internal class SvgFilterResult
{
    public string? Key { get; }

    public SKImageFilter Filter { get; }

    public SvgColourInterpolation ColorSpace { get; }

    public SvgFilterResult(string? key, SKImageFilter filter, SvgColourInterpolation colorSpace)
    {
        Key = key;
        Filter = filter;
        ColorSpace = colorSpace;
    }
}
