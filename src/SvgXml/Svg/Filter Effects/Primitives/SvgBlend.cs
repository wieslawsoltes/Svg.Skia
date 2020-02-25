using Xml;

namespace Svg.FilterEffects
{
    [Element("feBlend")]
    public class SvgBlend : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
