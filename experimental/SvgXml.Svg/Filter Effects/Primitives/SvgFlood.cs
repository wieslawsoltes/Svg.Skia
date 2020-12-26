using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects
{
    [Element("feFlood")]
    public class SvgFlood : SvgFilterPrimitive,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
    }
}
