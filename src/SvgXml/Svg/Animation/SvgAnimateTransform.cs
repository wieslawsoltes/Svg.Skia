using System;
using Xml;

namespace Svg
{
    [Element("animateTransform")]
    public class SvgAnimateTransform : SvgAnimationElement, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
