using System;
using Xml;

namespace Svg
{
    public interface ISvgCommonAttributes : IElement
    {
        [Attribute("id", SvgElement.SvgNamespace)]
        public string? Id
        {
            get => GetAttribute("id");
            set => SetAttribute("id", value);
        }

        [Attribute("base", SvgElement.XmlNamespace)]
        public string? Base
        {
            get => GetAttribute("base");
            set => SetAttribute("base", value);
        }

        [Attribute("lang", SvgElement.XmlNamespace)]
        public string? Lang
        {
            get => GetAttribute("lang");
            set => SetAttribute("lang", value);
        }

        [Attribute("space", SvgElement.XmlNamespace)]
        public string? Space
        {
            get => GetAttribute("space");
            set => SetAttribute("space", value);
        }
    }
}
