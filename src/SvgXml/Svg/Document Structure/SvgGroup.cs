using Xml;

namespace Svg
{
    [Element("g")]
    public class SvgGroup : SvgMarkerElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
    }
}
