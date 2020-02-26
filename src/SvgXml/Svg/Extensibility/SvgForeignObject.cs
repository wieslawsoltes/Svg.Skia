using Xml;

namespace Svg
{
    [Element("foreignObject")]
    public class SvgForeignObject : SvgVisualElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
