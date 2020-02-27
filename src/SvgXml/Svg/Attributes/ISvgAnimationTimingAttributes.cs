using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationTimingAttributes : IElement
    {
        [Attribute("begin", SvgElement.SvgNamespace)]
        public string? Begin
        {
            get => GetAttribute("begin");
            set => SetAttribute("begin", value);
        }

        [Attribute("dur", SvgElement.SvgNamespace)]
        public string? Dur
        {
            get => GetAttribute("dur");
            set => SetAttribute("dur", value);
        }

        [Attribute("end", SvgElement.SvgNamespace)]
        public string? End
        {
            get => GetAttribute("end");
            set => SetAttribute("end", value);
        }

        [Attribute("min", SvgElement.SvgNamespace)]
        public string? Min
        {
            get => GetAttribute("min");
            set => SetAttribute("min", value);
        }

        [Attribute("max", SvgElement.SvgNamespace)]
        public string? Max
        {
            get => GetAttribute("max");
            set => SetAttribute("max", value);
        }

        [Attribute("restart", SvgElement.SvgNamespace)]
        public string? Restart
        {
            get => GetAttribute("restart");
            set => SetAttribute("restart", value);
        }

        [Attribute("repeatCount", SvgElement.SvgNamespace)]
        public string? RepeatCount
        {
            get => GetAttribute("repeatCount");
            set => SetAttribute("repeatCount", value);
        }

        [Attribute("repeatDur", SvgElement.SvgNamespace)]
        public string? RepeatDur
        {
            get => GetAttribute("repeatDur");
            set => SetAttribute("repeatDur", value);
        }

        [Attribute("fill", SvgElement.SvgNamespace)]
        public string? Fill
        {
            get => GetAttribute("fill");
            set => SetAttribute("fill", value);
        }
    }
}
