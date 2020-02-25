using Xml;

namespace Svg
{
    [Element("image")]
    public class SvgImage : SvgVisualElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
