using Xml;

namespace Svg
{
    [Element("tspan")]
    public class SvgTextSpan : SvgTextPositioning,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
    }
}
