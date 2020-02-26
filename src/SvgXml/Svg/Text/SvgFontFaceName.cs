using System;
using Xml;

namespace Svg
{
    [Element("font-face-name")]
    public class SvgFontFaceName : SvgElement
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
