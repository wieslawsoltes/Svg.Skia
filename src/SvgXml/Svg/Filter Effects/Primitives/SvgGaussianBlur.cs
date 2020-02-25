using Xml;

namespace Svg.FilterEffects
{
    [Element("feGaussianBlur")]
    public class SvgGaussianBlur : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
