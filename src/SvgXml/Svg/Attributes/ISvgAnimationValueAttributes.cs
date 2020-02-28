using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationValueAttributes : IElement
    {
        [Attribute("calcMode", SvgElement.SvgNamespace)]
        public string? CalcMode
        {
            get => this.GetAttribute("calcMode");
            set => this.SetAttribute("calcMode", value);
        }

        [Attribute("values", SvgElement.SvgNamespace)]
        public string? Values
        {
            get => this.GetAttribute("values");
            set => this.SetAttribute("values", value);
        }

        [Attribute("keyTimes", SvgElement.SvgNamespace)]
        public string? KeyTimes
        {
            get => this.GetAttribute("keyTimes");
            set => this.SetAttribute("keyTimes", value);
        }

        [Attribute("keySplines", SvgElement.SvgNamespace)]
        public string? KeySplines
        {
            get => this.GetAttribute("keySplines");
            set => this.SetAttribute("keySplines", value);
        }

        [Attribute("from", SvgElement.SvgNamespace)]
        public string? From
        {
            get => this.GetAttribute("from");
            set => this.SetAttribute("from", value);
        }

        [Attribute("to", SvgElement.SvgNamespace)]
        public string? To
        {
            get => this.GetAttribute("to");
            set => this.SetAttribute("to", value);
        }

        [Attribute("by", SvgElement.SvgNamespace)]
        public string? By
        {
            get => this.GetAttribute("by");
            set => this.SetAttribute("by", value);
        }
    }
}
