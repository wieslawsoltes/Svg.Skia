using Xml;

namespace Svg
{
    [Element("altGlyph")]
    public class SvgAltGlyph : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
