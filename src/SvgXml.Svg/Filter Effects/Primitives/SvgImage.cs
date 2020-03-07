using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feImage")]
    public class SvgImage : SvgFilterPrimitive,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("preserveAspectRatio", SvgNamespace)]
        public string? AspectRatio
        {
            get => this.GetAttribute("preserveAspectRatio");
            set => this.SetAttribute("preserveAspectRatio", value);
        }

        [Attribute("href", XLinkNamespace)]
        public override string? Href
        {
            get => this.GetAttribute("href");
            set => this.SetAttribute("href", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "preserveAspectRatio":
                    AspectRatio = value;
                    break;
                case "href":
                    Href = value;
                    break;
            }
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (AspectRatio != null)
            {
                write($"{indent}{nameof(AspectRatio)}: \"{AspectRatio}\"");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
