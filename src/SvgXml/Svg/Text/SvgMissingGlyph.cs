using Xml;

namespace Svg
{
    [Element("missing-glyph")]
    public class SvgMissingGlyph : SvgGlyph, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
