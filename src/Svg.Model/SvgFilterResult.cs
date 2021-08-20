using Svg.DataTypes;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp;
#endif

namespace Svg.Model
{
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
}
