using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationEventAttributes : IElement
    {
        [Attribute("onbegin", SvgElement.SvgNamespace)]
        public string? OnBegin
        {
            get => this.GetAttribute("onbegin");
            set => this.SetAttribute("onbegin", value);
        }

        [Attribute("onend", SvgElement.SvgNamespace)]
        public string? OnEnd
        {
            get => this.GetAttribute("onend");
            set => this.SetAttribute("onend", value);
        }

        [Attribute("onrepeat", SvgElement.SvgNamespace)]
        public string? OnRepeat
        {
            get => this.GetAttribute("onrepeat");
            set => this.SetAttribute("onrepeat", value);
        }

        [Attribute("onload", SvgElement.SvgNamespace)]
        public string? OnLoad
        {
            get => this.GetAttribute("onload");
            set => this.SetAttribute("onload", value);
        }
    }
}
