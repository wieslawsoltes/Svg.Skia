using System;
using Xml;

namespace Svg
{
    [Element("animateColor")]
    public class SvgAnimateColor : SvgAnimationElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
