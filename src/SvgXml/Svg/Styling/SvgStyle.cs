using System;
using Xml;

namespace Svg
{
    [Element("style")]
    public class SvgStyle : SvgElement
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
