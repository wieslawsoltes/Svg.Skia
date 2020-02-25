using Xml;

namespace Svg
{
    [Element("svg")]
    public class SvgFragment : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
