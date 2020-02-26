using System;
using Xml;

namespace Svg
{
    public interface ISvgCommonAttributes : IElement
    {
        [Attribute("id", SvgAttributes.SvgNamespace)]
        public string? Id
        {
            get => GetAttribute("id");
            set => SetAttribute("id", value);
        }

        [Attribute("base", SvgAttributes.XmlNamespace)]
        public string? Base
        {
            get => GetAttribute("base");
            set => SetAttribute("base", value);
        }

        [Attribute("lang", SvgAttributes.XmlNamespace)]
        public string? Lang
        {
            get => GetAttribute("lang");
            set => SetAttribute("lang", value);
        }

        [Attribute("space", SvgAttributes.XmlNamespace)]
        public string? Space
        {
            get => GetAttribute("space");
            set => SetAttribute("space", value);
        }
    }
}
