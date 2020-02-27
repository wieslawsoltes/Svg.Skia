using System;
using Xml;

namespace Svg
{
    [Element("animateColor")]
    public class SvgAnimateColor : SvgAnimationElement,
                                   ISvgPresentationAttributes,
                                   ISvgTestsAttributes,
                                   ISvgResourcesAttributes,
                                   ISvgXLinkAttributes,
                                   ISvgAnimationEventAttributes,
                                   ISvgAnimationAttributeTargetAttributes,
                                   ISvgAnimationTimingAttributes,
                                   ISvgAnimationValueAattributes,
                                   ISvgAnimationAdditionAttributes
    {
    }
}
