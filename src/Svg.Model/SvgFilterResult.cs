using Svg.DataTypes;
using ShimSkiaSharp;

namespace Svg.Model;

internal class SvgFilterResult(string? key, SKImageFilter filter, SvgColourInterpolation colorSpace)
{
    public string? Key { get; } = key;

    public SKImageFilter Filter { get; } = filter;

    public SvgColourInterpolation ColorSpace { get; } = colorSpace;
}
