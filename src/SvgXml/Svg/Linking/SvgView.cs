using System;
using Xml;

namespace Svg
{
    [Element("view")]
    public class SvgView : SvgElement, ISvgResourcesAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
