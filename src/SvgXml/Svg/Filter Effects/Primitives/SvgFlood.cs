using Xml;

namespace Svg.FilterEffects
{
    [Element("feFlood")]
    public class SvgFlood : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
