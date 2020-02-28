using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationEventAttributes : IElement
    {
        [Attribute("onbegin", SvgElement.SvgNamespace)]
        public string? OnBegin
        {
            get => GetAttribute("onbegin");
            set => SetAttribute("onbegin", value);
        }

        [Attribute("onend", SvgElement.SvgNamespace)]
        public string? OnEnd
        {
            get => GetAttribute("onend");
            set => SetAttribute("onend", value);
        }

        [Attribute("onrepeat", SvgElement.SvgNamespace)]
        public string? OnRepeat
        {
            get => GetAttribute("onrepeat");
            set => SetAttribute("onrepeat", value);
        }

        [Attribute("onload", SvgElement.SvgNamespace)]
        public string? OnLoad
        {
            get => GetAttribute("onload");
            set => SetAttribute("onload", value);
        }
    }
}
