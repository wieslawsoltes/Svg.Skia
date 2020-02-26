using Xml;

namespace Svg
{
    [Element("tref")]
    public class SvgTextRef : SvgTextBase, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
