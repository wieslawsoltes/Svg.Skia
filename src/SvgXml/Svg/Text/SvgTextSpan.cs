using Xml;

namespace Svg
{
    [Element("tspan")]
    public class SvgTextSpan : SvgTextBase, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
