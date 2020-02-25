using Xml;

namespace Svg.FilterEffects
{
    [Element("feComposite")]
    public class SvgComposite : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
