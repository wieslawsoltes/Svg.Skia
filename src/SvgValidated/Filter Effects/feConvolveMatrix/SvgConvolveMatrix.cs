
namespace SvgValidated.FilterEffects
{
    public class SvgConvolveMatrix : SvgFilterPrimitive
    {
        public SvgNumberCollection Order { get; set; }
        public SvgNumberCollection KernelMatrix { get; set; }
        public float Divisor { get; set; }
        public float Bias { get; set; }
        public int TargetX { get; set; }
        public int TargetY { get; set; }
        public SvgEdgeMode EdgeMode { get; set; }
        public SvgNumberCollection KernelUnitLength { get; set; }
        public bool PreserveAlpha { get; set; }
    }
}
