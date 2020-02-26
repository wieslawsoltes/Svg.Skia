using System;
using Xml;

namespace Svg
{
    [Element("font")]
    public class SvgFont : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
