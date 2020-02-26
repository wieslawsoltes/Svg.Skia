using System;
using Xml;

namespace Svg
{
    [Element("cursor")]
    public class SvgCursor : SvgElement, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
