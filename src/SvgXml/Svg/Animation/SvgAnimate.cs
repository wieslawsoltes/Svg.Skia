using System;
using Xml;

namespace Svg
{
    [Element("animate")]
    public class SvgAnimate : SvgAnimationElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);
            // TODO:
        }
    }
}
