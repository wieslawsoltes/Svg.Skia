using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationValueAattributes : IElement
    {
        [Attribute("calcMode", SvgAttributes.SvgNamespace)]
        public string? CalcMode
        {
            get => GetAttribute("calcMode");
            set => SetAttribute("calcMode", value);
        }

        [Attribute("values", SvgAttributes.SvgNamespace)]
        public string? Values
        {
            get => GetAttribute("values");
            set => SetAttribute("values", value);
        }

        [Attribute("keyTimes", SvgAttributes.SvgNamespace)]
        public string? KeyTimes
        {
            get => GetAttribute("keyTimes");
            set => SetAttribute("keyTimes", value);
        }

        [Attribute("keySplines", SvgAttributes.SvgNamespace)]
        public string? KeySplines
        {
            get => GetAttribute("keySplines");
            set => SetAttribute("keySplines", value);
        }

        [Attribute("from", SvgAttributes.SvgNamespace)]
        public string? From
        {
            get => GetAttribute("from");
            set => SetAttribute("from", value);
        }

        [Attribute("to", SvgAttributes.SvgNamespace)]
        public string? To
        {
            get => GetAttribute("to");
            set => SetAttribute("to", value);
        }

        [Attribute("by", SvgAttributes.SvgNamespace)]
        public string? By
        {
            get => GetAttribute("by");
            set => SetAttribute("by", value);
        }
    }
}
