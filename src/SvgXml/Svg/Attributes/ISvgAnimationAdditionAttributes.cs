using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationAdditionAttributes : IElement
    {
        [Attribute("additive", SvgElement.SvgNamespace)]
        public string? Additive
        {
            get => GetAttribute("additive");
            set => SetAttribute("additive", value);
        }

        [Attribute("accumulate", SvgElement.SvgNamespace)]
        public string? Accumulate
        {
            get => GetAttribute("accumulate");
            set => SetAttribute("accumulate", value);
        }
    }
}
