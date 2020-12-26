using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
{
    [Element("missing-glyph")]
    public class SvgMissingGlyph : SvgGlyph,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
    }
}
