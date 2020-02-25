using Xml;

namespace Svg.FilterEffects
{
    [Element("feComponentTransfer")]
    public class SvgComponentTransfer : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
