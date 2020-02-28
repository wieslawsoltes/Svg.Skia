using Xml;

namespace Svg
{
    [Element("text")]
    public class SvgText : SvgTextPositioning,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
    }
}
