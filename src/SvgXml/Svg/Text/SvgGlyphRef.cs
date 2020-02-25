using Xml;

namespace Svg
{
    [Element("glyphRef")]
    public class SvgGlyphRef : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
