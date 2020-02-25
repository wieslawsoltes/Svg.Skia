using Xml;

namespace Svg
{
    [Element("glyph")]
    public class SvgGlyph : SvgPathBasedElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
