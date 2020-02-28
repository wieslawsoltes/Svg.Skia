using Xml;

namespace Svg
{
    [Element("polyline")]
    public class SvgPolyline : SvgPolygon,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
    }
}
