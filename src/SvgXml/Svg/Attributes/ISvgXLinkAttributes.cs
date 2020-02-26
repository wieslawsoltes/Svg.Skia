using System;
using Xml;

namespace Svg
{
    public interface ISvgXLinkAttributes : IElement
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

        [Attribute("type", SvgAttributes.XLinkNamespace)]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        [Attribute("role", SvgAttributes.XLinkNamespace)]
        public string? Role
        {
            get => GetAttribute("role");
            set => SetAttribute("role", value);
        }

        [Attribute("arcrole", SvgAttributes.XLinkNamespace)]
        public string? Arcrole
        {
            get => GetAttribute("arcrole");
            set => SetAttribute("arcrole", value);
        }

        [Attribute("title", SvgAttributes.XLinkNamespace)]
        public string? Title
        {
            get => GetAttribute("title");
            set => SetAttribute("title", value);
        }
    }
}
