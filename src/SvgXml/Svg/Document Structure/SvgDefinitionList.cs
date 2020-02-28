using Xml;

namespace Svg
{
    [Element("defs")]
    public class SvgDefinitionList : SvgElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
    }
}
