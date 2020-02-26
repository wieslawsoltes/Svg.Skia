using System;
using Xml;

namespace Svg
{
    [Element("font-face-format")]
    public class SvgFontFaceFormat : SvgElement
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
