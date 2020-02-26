using System;
using Xml;

namespace Svg
{
    [Element("hkern")]
    public class SvgHorizontalKern : SvgKern
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
