
namespace SvgValidated.FilterEffects
{
    public class SvgMorphology : SvgFilterPrimitive
    {
        public SvgMorphologyOperator Operator { get; set; }
        public SvgNumberCollection Radius { get; set; }
    }
}
