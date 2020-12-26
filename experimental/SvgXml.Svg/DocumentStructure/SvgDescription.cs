using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.DocumentStructure
{
    [Element("desc")]
    public class SvgDescription : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgStylableAttributes
    {
    }
}
