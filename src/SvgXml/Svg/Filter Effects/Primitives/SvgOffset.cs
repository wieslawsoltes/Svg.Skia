using Xml;

namespace Svg.FilterEffects
{
    [Element("feOffset")]
    public class SvgOffset : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
