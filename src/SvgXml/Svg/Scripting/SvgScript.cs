using System;
using Xml;

namespace Svg
{
    [Element("script")]
    public class SvgScript : SvgElement,
        ISvgCommonAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("type", SvgNamespace)]
        public string? Type
        {
            get => this.GetAttribute("type");
            set => this.SetAttribute("type", value);
        }

        [Attribute("href", XLinkNamespace)]
        public string? Href
        {
            get => this.GetAttribute("href");
            set => this.SetAttribute("href", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Type != null)
            {
                write($"{indent}{nameof(Type)}: \"{Type}\"");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
