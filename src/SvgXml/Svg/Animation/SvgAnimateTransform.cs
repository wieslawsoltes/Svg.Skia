using System;
using Xml;

namespace Svg
{
    [Element("animateTransform")]
    public class SvgAnimateTransform : SvgAnimationElement,
                                       ISvgTestsAttributes,
                                       ISvgResourcesAttributes,
                                       ISvgXLinkAttributes,
                                       ISvgAnimationEventAttributes,
                                       ISvgAnimationAttributeTargetAttributes,
                                       ISvgAnimationTimingAttributes,
                                       ISvgAnimationValueAattributes,
                                       ISvgAnimationAdditionAttributes
    {
        [Attribute("type", SvgElement.SvgNamespace)]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Type != null)
            {
                Console.WriteLine($"{indent}{nameof(Type)}='{Type}'");
            }
        }
    }
}
