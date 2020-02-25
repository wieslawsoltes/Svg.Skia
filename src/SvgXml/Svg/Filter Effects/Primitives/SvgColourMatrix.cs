using Xml;

namespace Svg.FilterEffects
{
    [Element("feColorMatrix")]
    public class SvgColourMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
