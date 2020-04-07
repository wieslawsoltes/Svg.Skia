
namespace SvgValidated.FilterEffects
{
    public class SvgComposite : SvgFilterPrimitive
    {
        public SvgCompositeOperator Operator { get; set; }
        public float K1 { get; set; }
        public float K2 { get; set; }
        public float K3 { get; set; }
        public float K4 { get; set; }
        public string Input2 { get; set; }
    }
}
