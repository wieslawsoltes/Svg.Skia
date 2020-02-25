using Xml;

namespace Svg.FilterEffects
{
    [Element("feConvolveMatrix")]
    public class SvgConvolveMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
