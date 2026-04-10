using ShimSkiaSharp;
using Svg.FilterEffects;

namespace Svg.Skia;

internal sealed class SvgSceneFilterPrimitiveContext
{
    public SvgSceneFilterPrimitiveContext(SvgFilterPrimitive svgFilterPrimitive)
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
