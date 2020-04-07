
namespace SvgValidated.FilterEffects
{
    public class SvgDisplacementMap : SvgFilterPrimitive
    {
        public float Scale { get; set; }
        public SvgChannelSelector XChannelSelector { get; set; }
        public SvgChannelSelector YChannelSelector { get; set; }
        public string Input2 { get; set; }
    }
}
