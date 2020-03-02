using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationTimingAttributes : IElement
    {
        [Attribute("begin", SvgElement.SvgNamespace)]
        public string? Begin
        {
            get => this.GetAttribute("begin");
            set => this.SetAttribute("begin", value);
        }

        [Attribute("dur", SvgElement.SvgNamespace)]
        public string? Dur
        {
            get => this.GetAttribute("dur");
            set => this.SetAttribute("dur", value);
        }

        [Attribute("end", SvgElement.SvgNamespace)]
        public string? End
        {
            get => this.GetAttribute("end");
            set => this.SetAttribute("end", value);
        }

        [Attribute("min", SvgElement.SvgNamespace)]
        public string? Min
        {
            get => this.GetAttribute("min");
            set => this.SetAttribute("min", value);
        }

        [Attribute("max", SvgElement.SvgNamespace)]
        public string? Max
        {
            get => this.GetAttribute("max");
            set => this.SetAttribute("max", value);
        }

        [Attribute("restart", SvgElement.SvgNamespace)]
        public string? Restart
        {
            get => this.GetAttribute("restart");
            set => this.SetAttribute("restart", value);
        }

        [Attribute("repeatCount", SvgElement.SvgNamespace)]
        public string? RepeatCount
        {
            get => this.GetAttribute("repeatCount");
            set => this.SetAttribute("repeatCount", value);
        }

        [Attribute("repeatDur", SvgElement.SvgNamespace)]
        public string? RepeatDur
        {
            get => this.GetAttribute("repeatDur");
            set => this.SetAttribute("repeatDur", value);
        }

        [Attribute("fill", SvgElement.SvgNamespace)]
        public string? Fill
        {
            get => this.GetAttribute("fill");
            set => this.SetAttribute("fill", value);
        }
    }
}
