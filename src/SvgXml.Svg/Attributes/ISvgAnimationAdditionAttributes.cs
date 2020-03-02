using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationAdditionAttributes : IElement
    {
        [Attribute("additive", SvgElement.SvgNamespace)]
        public string? Additive
        {
            get => this.GetAttribute("additive");
            set => this.SetAttribute("additive", value);
        }

        [Attribute("accumulate", SvgElement.SvgNamespace)]
        public string? Accumulate
        {
            get => this.GetAttribute("accumulate");
            set => this.SetAttribute("accumulate", value);
        }
    }
}
