using System;
using Xml;

namespace Svg
{
    [Element("altGlyphItem")]
    public class SvgAltGlyphItem : SvgElement
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
