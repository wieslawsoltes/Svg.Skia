using Xml;

namespace Svg
{
    [Element("a")]
    public class SvgAnchor : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
