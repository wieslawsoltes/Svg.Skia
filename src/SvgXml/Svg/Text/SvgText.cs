using Xml;

namespace Svg
{
    [Element("text")]
    public class SvgText : SvgTextBase, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
