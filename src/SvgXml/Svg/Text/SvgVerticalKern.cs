using System;
using Xml;

namespace Svg
{
    [Element("vkern")]
    public class SvgVerticalKern : SvgKern
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
