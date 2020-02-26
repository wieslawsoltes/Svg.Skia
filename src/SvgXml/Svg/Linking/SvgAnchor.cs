using System;
using Xml;

namespace Svg
{
    [Element("a")]
    public class SvgAnchor : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        [Attribute("href", SvgAttributes.XLinkNamespace)]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
        }

        [Attribute("show", SvgAttributes.XLinkNamespace)]
        public string? Show
        {
            get => GetAttribute("show");
            set => SetAttribute("show", value);
        }

        [Attribute("actuate", SvgAttributes.XLinkNamespace)]
        public string? Actuate
        {
            get => GetAttribute("actuate");
            set => SetAttribute("actuate", value);
        }

        [Attribute("target", SvgAttributes.SvgNamespace)]
        public string? Target
        {
            get => GetAttribute("target");
            set => SetAttribute("target", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
            if (Show != null)
            {
                write($"{indent}{nameof(Show)}: \"{Show}\"");
            }
            if (Actuate != null)
            {
                write($"{indent}{nameof(Actuate)}: \"{Actuate}\"");
            }
            if (Target != null)
            {
                write($"{indent}{nameof(Target)}: \"{Target}\"");
            }
        }
    }
}
