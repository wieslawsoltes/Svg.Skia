using System;
using Xml;

namespace Svg
{
    public interface ISvgStylableAttributes : IElement
    {
        [Attribute("class", SvgAttributes.SvgNamespace)]
        public string? Class
        {
            get => GetAttribute("class");
            set => SetAttribute("class", value);
        }

        [Attribute("style", SvgAttributes.SvgNamespace)]
        public string? Style
        {
            get => GetAttribute("style");
            set => SetAttribute("style", value);
        }
    }
}
