using Xml;

namespace Svg.FilterEffects
{
    [Element("feMerge")]
    public class SvgMerge : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
