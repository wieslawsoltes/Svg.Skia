using System;
using Xml;

namespace Svg
{
    [Element("script")]
    public class SvgScript : SvgElement, ISvgResourcesAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
