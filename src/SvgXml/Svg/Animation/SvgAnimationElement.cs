using System;

namespace Svg
{
    public abstract class SvgAnimationElement : SvgElement
    {
        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (this is ISvgAnimationAdditionAttributes svgAnimationAdditionAttributes)
            {
                PrintAnimationAdditionAttributes(svgAnimationAdditionAttributes, write, indent);
            }
            if (this is ISvgAnimationAttributeTargetAttributes svgAnimationAttributeTargetAttributes)
            {
                PrintAnimationAttributeTargetAttributes(svgAnimationAttributeTargetAttributes, write, indent);
            }
            if (this is ISvgAnimationEventAttributes svgAnimationEventAttributes)
            {
                PrintAnimationEventAttributes(svgAnimationEventAttributes, write, indent);
            }
            if (this is ISvgAnimationTimingAttributes svgAnimationTimingAttributes)
            {
                PrintAnimationTimingAttributes(svgAnimationTimingAttributes, write, indent);
            }
            if (this is ISvgAnimationValueAttributes svgAnimationValueAttributes)
            {
                PrintAnimationValueAttributes(svgAnimationValueAttributes, write, indent);
            }
        }
    }
}
