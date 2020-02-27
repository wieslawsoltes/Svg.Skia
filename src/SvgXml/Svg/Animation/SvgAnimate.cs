using System;
using Xml;

namespace Svg
{
    [Element("animate")]
    public class SvgAnimate : SvgAnimationElement,
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
