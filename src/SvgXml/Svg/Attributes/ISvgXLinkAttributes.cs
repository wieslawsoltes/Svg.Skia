using System;
using Xml;

namespace Svg
{
    public interface ISvgXLinkAttributes : IElement
    {
        [Attribute("href", SvgElement.XLinkNamespace)]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
        }

        [Attribute("show", SvgElement.XLinkNamespace)]
        public string? Show
        {
            get => GetAttribute("show");
            set => SetAttribute("show", value);
        }

        [Attribute("actuate", SvgElement.XLinkNamespace)]
        public string? Actuate
        {
            get => GetAttribute("actuate");
            set => SetAttribute("actuate", value);
        }

        [Attribute("type", SvgElement.XLinkNamespace)]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        [Attribute("role", SvgElement.XLinkNamespace)]
        public string? Role
        {
            get => GetAttribute("role");
            set => SetAttribute("role", value);
        }

        [Attribute("arcrole", SvgElement.XLinkNamespace)]
        public string? Arcrole
        {
            get => GetAttribute("arcrole");
            set => SetAttribute("arcrole", value);
        }

        [Attribute("title", SvgElement.XLinkNamespace)]
        public string? Title
        {
            get => GetAttribute("title");
            set => SetAttribute("title", value);
        }
    }
}
