using Xml;

namespace Svg.FilterEffects
{
    [Element("feDisplacementMap")]
    public class SvgDisplacementMap : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
