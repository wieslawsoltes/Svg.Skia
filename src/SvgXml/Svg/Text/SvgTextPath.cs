using Xml;

namespace Svg
{
    [Element("textPath")]
    public class SvgTextPath : SvgTextBase, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
