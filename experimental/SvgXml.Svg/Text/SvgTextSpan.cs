using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
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
