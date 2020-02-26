using System;
using Xml;

namespace Svg
{
    [Element("altGlyphDef")]
    public class SvgAltGlyphDef : SvgElement
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
