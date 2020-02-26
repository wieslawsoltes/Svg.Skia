using System;
using Xml;

namespace Svg
{
    [Element("script")]
    public class SvgScript : SvgElement, ISvgResourcesAttributes
    {
        [Attribute("type")]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        [Attribute("href")]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Type != null)
            {
                write($"{indent}{nameof(Type)}='{Type}'");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
