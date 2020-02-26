using System;
using Xml;

namespace Svg
{
    [Element("glyphRef")]
    public class SvgGlyphRef : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
