using System;
using Xml;

namespace Svg
{
    [Element("missing-glyph")]
    public class SvgMissingGlyph : SvgGlyph, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
