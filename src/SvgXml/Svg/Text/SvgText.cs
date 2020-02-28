using Xml;

namespace Svg
{
    [Element("text")]
    public class SvgText : SvgTextPositioning,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
    }
}
