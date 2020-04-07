
namespace Svg.FilterEffects
{
    public class SvgTurbulence : SvgFilterPrimitive
    {
        public SvgNumberCollection BaseFrequency { get; set; }
        public int NumOctaves { get; set; }
        public float Seed { get; set; }
        public SvgStitchType StitchTiles { get; set; }
        public SvgTurbulenceType Type { get; set; }
    }
}
