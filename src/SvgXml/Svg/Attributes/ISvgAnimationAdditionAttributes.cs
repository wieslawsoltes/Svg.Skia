using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationAdditionAttributes : IElement
    {
        [Attribute("additive", SvgAttributes.SvgNamespace)]
        public string? Additive
        {
            get => GetAttribute("additive");
            set => SetAttribute("additive", value);
        }

        [Attribute("accumulate", SvgAttributes.SvgNamespace)]
        public string? Accumulate
        {
            get => GetAttribute("accumulate");
            set => SetAttribute("accumulate", value);
        }
    }
}
