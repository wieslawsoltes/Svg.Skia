
namespace SvgValidated.FilterEffects
{
    public class SvgDiffuseLighting : SvgFilterPrimitive
    {
        public float SurfaceScale { get; set; }
        public float DiffuseConstant { get; set; }
        public SvgNumberCollection KernelUnitLength { get; set; }
        public SvgPaintServer LightingColor { get; set; }
    }
}
