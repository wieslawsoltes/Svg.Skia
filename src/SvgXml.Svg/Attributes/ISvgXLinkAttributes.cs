using System;
using Xml;

namespace Svg
{
    public interface ISvgXLinkAttributes : IElement
    {
        [Attribute("href", SvgElement.XLinkNamespace)]
        public string? Href
        {
            get => this.GetAttribute("href", false, null);
            set => this.SetAttribute("href", value);
        }

        [Attribute("show", SvgElement.XLinkNamespace)]
        public string? Show
        {
            get => this.GetAttribute("show", false, null);
            set => this.SetAttribute("show", value);
        }

        [Attribute("actuate", SvgElement.XLinkNamespace)]
        public string? Actuate
        {
            get => this.GetAttribute("actuate", false, null);
            set => this.SetAttribute("actuate", value);
        }

        [Attribute("type", SvgElement.XLinkNamespace)]
        public string? Type
        {
            get => this.GetAttribute("type", false, null);
            set => this.SetAttribute("type", value);
        }

        [Attribute("role", SvgElement.XLinkNamespace)]
        public string? Role
        {
            get => this.GetAttribute("role", false, null);
            set => this.SetAttribute("role", value);
        }

        [Attribute("arcrole", SvgElement.XLinkNamespace)]
        public string? Arcrole
        {
            get => this.GetAttribute("arcrole", false, null);
            set => this.SetAttribute("arcrole", value);
        }

        [Attribute("title", SvgElement.XLinkNamespace)]
        public string? Title
        {
            get => this.GetAttribute("title");
            set => this.SetAttribute("title", value);
        }
    }
}
