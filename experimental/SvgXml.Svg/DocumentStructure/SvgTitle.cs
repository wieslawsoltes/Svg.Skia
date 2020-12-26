using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.DocumentStructure
{
    [Element("title")]
    public class SvgTitle : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgStylableAttributes
    {
    }
}
