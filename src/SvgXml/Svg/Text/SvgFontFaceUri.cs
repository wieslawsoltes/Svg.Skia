using System;
using Xml;

namespace Svg
{
    [Element("font-face-uri")]
    public class SvgFontFaceUri : SvgElement
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
