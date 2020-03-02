using System;
using Xml;

namespace Svg
{
    [Element("animateTransform")]
    public class SvgAnimateTransform : SvgAnimationElement,
        ISvgCommonAttributes,
        ISvgTestsAttributes,
        ISvgResourcesAttributes,
        ISvgXLinkAttributes,
        ISvgAnimationEventAttributes,
        ISvgAnimationAttributeTargetAttributes,
        ISvgAnimationTimingAttributes,
        ISvgAnimationValueAttributes,
        ISvgAnimationAdditionAttributes
    {
        [Attribute("type", SvgNamespace)]
        public string? Type
        {
            get => this.GetAttribute("type");
            set => this.SetAttribute("type", value);
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
