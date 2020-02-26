using Xml;

namespace Svg
{
    [Element("cursor")]
    public class SvgCursor : SvgElement, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
