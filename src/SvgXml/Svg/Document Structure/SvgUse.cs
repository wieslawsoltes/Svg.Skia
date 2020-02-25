using Xml;

namespace Svg
{
    [Element("use")]
    public class SvgUse : SvgVisualElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
