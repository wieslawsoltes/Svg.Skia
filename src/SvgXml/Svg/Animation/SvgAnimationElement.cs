using System;
using Xml;

namespace Svg
{
    public abstract class SvgAnimationElement : SvgElement
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (this is ISvgAnimationAdditionAttributes svgAnimationAdditionAttributes)
            {
                svgAnimationAdditionAttributes.PrintAnimationAdditionAttributes(write, indent);
            }
            if (this is ISvgAnimationAttributeTargetAttributes svgAnimationAttributeTargetAttributes)
            {
                svgAnimationAttributeTargetAttributes.PrintAnimationAttributeTargetAttributes(write, indent);
            }
            if (this is ISvgAnimationEventAttributes svgAnimationEventAttributes)
            {
                svgAnimationEventAttributes.PrintAnimationEventAttributes(write, indent);
            }
            if (this is ISvgAnimationTimingAttributes svgAnimationTimingAttributes)
            {
                svgAnimationTimingAttributes.PrintAnimationTimingAttributes(write, indent);
            }
            if (this is ISvgAnimationValueAattributes svgAnimationValueAattributes)
            {
                svgAnimationValueAattributes.PrintAnimationValueAattributes(write, indent);
            }
        }
    }
}
