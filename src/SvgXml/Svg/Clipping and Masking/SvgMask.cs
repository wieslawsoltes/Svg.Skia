using Xml;

namespace Svg
{
    [Element("mask")]
    public class SvgMask : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
