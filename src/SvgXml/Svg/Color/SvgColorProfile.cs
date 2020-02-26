using System;
using Xml;

namespace Svg
{
    [Element("color-profile")]
    public class SvgColorProfile : SvgElement
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
