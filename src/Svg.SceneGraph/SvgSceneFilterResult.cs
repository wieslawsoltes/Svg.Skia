using ShimSkiaSharp;
using Svg.DataTypes;

namespace Svg.Skia;

internal sealed class SvgSceneFilterResult
{
    public SvgSceneFilterResult(string? key, SKImageFilter filter, SvgColourInterpolation colorSpace)
    {
        Key = key;
        Filter = filter;
        ColorSpace = colorSpace;
    }

    public string? Key { get; }

    public SKImageFilter Filter { get; }

    public SvgColourInterpolation ColorSpace { get; }
}
