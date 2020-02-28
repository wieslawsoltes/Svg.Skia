using Xml;

namespace Svg
{
    [Element("polyline")]
    public class SvgPolyline : SvgPolygon,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
    }
}
