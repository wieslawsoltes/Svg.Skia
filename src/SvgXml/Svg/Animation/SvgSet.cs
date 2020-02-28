using System;
using Xml;

namespace Svg
{
    [Element("set")]
    public class SvgSet : SvgAnimationElement,
        ISvgTestsAttributes,
        ISvgResourcesAttributes,
        ISvgXLinkAttributes,
        ISvgAnimationEventAttributes,
        ISvgAnimationAttributeTargetAttributes,
        ISvgAnimationTimingAttributes
    {
        [Attribute("to", SvgNamespace)]
        public string? To
        {
            get => GetAttribute("to");
            set => SetAttribute("to", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (To != null)
            {
                Console.WriteLine($"{indent}{nameof(To)}='{To}'");
            }
        }
    }
}
