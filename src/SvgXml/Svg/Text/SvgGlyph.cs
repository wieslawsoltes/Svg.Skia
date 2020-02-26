using System;
using Xml;

namespace Svg
{
    [Element("glyph")]
    public class SvgGlyph : SvgPathBasedElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
