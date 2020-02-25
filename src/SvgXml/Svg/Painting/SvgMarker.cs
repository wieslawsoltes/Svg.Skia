using Xml;

namespace Svg
{
    [Element("marker")]
    public class SvgMarker : SvgPathBasedElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
