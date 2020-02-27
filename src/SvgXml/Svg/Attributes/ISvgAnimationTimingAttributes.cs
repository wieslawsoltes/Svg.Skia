using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationTimingAttributes : IElement
    {
        [Attribute("begin", SvgAttributes.SvgNamespace)]
        public string? Begin
        {
            get => GetAttribute("begin");
            set => SetAttribute("begin", value);
        }

        [Attribute("dur", SvgAttributes.SvgNamespace)]
        public string? Dur
        {
            get => GetAttribute("dur");
            set => SetAttribute("dur", value);
        }

        [Attribute("end", SvgAttributes.SvgNamespace)]
        public string? End
        {
            get => GetAttribute("end");
            set => SetAttribute("end", value);
        }

        [Attribute("min", SvgAttributes.SvgNamespace)]
        public string? Min
        {
            get => GetAttribute("min");
            set => SetAttribute("min", value);
        }

        [Attribute("max", SvgAttributes.SvgNamespace)]
        public string? Max
        {
            get => GetAttribute("max");
            set => SetAttribute("max", value);
        }

        [Attribute("restart", SvgAttributes.SvgNamespace)]
        public string? Restart
        {
            get => GetAttribute("restart");
            set => SetAttribute("restart", value);
        }

        [Attribute("repeatCount", SvgAttributes.SvgNamespace)]
        public string? RepeatCount
        {
            get => GetAttribute("repeatCount");
            set => SetAttribute("repeatCount", value);
        }

        [Attribute("repeatDur", SvgAttributes.SvgNamespace)]
        public string? RepeatDur
        {
            get => GetAttribute("repeatDur");
            set => SetAttribute("repeatDur", value);
        }

        [Attribute("fill", SvgAttributes.SvgNamespace)]
        public string? Fill
        {
            get => GetAttribute("fill");
            set => SetAttribute("fill", value);
        }
    }
}
