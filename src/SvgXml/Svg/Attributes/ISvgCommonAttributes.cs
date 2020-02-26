using System;
using Xml;

namespace Svg
{
    public interface ISvgCommonAttributes : IElement
    {
        [Attribute("id")]
        public string? Id
        {
            get => GetAttribute("id");
            set => SetAttribute("id", value);
        }

        [Attribute("base")]
        public string? Base
        {
            get => GetAttribute("base");
            set => SetAttribute("base", value);
        }

        [Attribute("lang")]
        public string? Lang
        {
            get => GetAttribute("lang");
            set => SetAttribute("lang", value);
        }

        [Attribute("space")]
        public string? Space
        {
            get => GetAttribute("space");
            set => SetAttribute("space", value);
        }
    }
}
