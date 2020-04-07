
namespace Svg.FilterEffects
{
    public class SvgSpecularLighting : SvgFilterPrimitive
    {
        public float SurfaceScale { get; set; }
        public float SpecularConstant { get; set; }
        public float SpecularExponent { get; set; }
        public SvgNumberCollection KernelUnitLength { get; set; }
        public SvgPaintServer LightingColor { get; set; }
    }
}
