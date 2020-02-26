using System;
using Xml;

namespace Svg
{
    [Element("mpath")]
    public class SvgMotionPath : SvgElement, ISvgResourcesAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
