using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationValueAttributes : IElement
    {
        [Attribute("calcMode", SvgElement.SvgNamespace)]
        public string? CalcMode
        {
            get => GetAttribute("calcMode");
            set => SetAttribute("calcMode", value);
        }

        [Attribute("values", SvgElement.SvgNamespace)]
        public string? Values
        {
            get => GetAttribute("values");
            set => SetAttribute("values", value);
        }

        [Attribute("keyTimes", SvgElement.SvgNamespace)]
        public string? KeyTimes
        {
            get => GetAttribute("keyTimes");
            set => SetAttribute("keyTimes", value);
        }

        [Attribute("keySplines", SvgElement.SvgNamespace)]
        public string? KeySplines
        {
            get => GetAttribute("keySplines");
            set => SetAttribute("keySplines", value);
        }

        [Attribute("from", SvgElement.SvgNamespace)]
        public string? From
        {
            get => GetAttribute("from");
            set => SetAttribute("from", value);
        }

        [Attribute("to", SvgElement.SvgNamespace)]
        public string? To
        {
            get => GetAttribute("to");
            set => SetAttribute("to", value);
        }

        [Attribute("by", SvgElement.SvgNamespace)]
        public string? By
        {
            get => GetAttribute("by");
            set => SetAttribute("by", value);
        }
    }
}
