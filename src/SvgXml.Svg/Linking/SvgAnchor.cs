using System;
using Xml;

namespace Svg
{
    [Element("a")]
    public class SvgAnchor : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
        // ISvgTransformableAttributes

        [Attribute("transform", SvgElement.SvgNamespace)]
        public string? Transform
        {
            get => this.GetAttribute("transform", false, null);
            set => this.SetAttribute("transform", value);
        }

        // SvgAnchor

        [Attribute("href", XLinkNamespace)]
        public string? Href
        {
            get => this.GetAttribute("href");
            set => this.SetAttribute("href", value);
        }

        [Attribute("show", XLinkNamespace)]
        public string? Show
        {
            get => this.GetAttribute("show");
            set => this.SetAttribute("show", value);
        }

        [Attribute("actuate", XLinkNamespace)]
        public string? Actuate
        {
            get => this.GetAttribute("actuate");
            set => this.SetAttribute("actuate", value);
        }

        [Attribute("target", SvgNamespace)]
        public string? Target
        {
            get => this.GetAttribute("target");
            set => this.SetAttribute("target", value);
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
