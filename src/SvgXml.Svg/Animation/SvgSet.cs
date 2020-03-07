using System;
using Xml;

namespace Svg
{
    [Element("set")]
    public class SvgSet : SvgAnimationElement,
        ISvgCommonAttributes,
        ISvgTestsAttributes,
        ISvgResourcesAttributes,
        ISvgXLinkAttributes,
        ISvgAnimationEventAttributes,
        ISvgAnimationAttributeTargetAttributes,
        ISvgAnimationTimingAttributes
    {
        [Attribute("to", SvgNamespace)]
        public override string? To
        {
            get => this.GetAttribute("to");
            set => this.SetAttribute("to", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "to":
                    To = value;
                    break;
            }
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
