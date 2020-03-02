using System;
using Xml;

namespace Svg
{
    public interface ISvgTestsAttributes : IElement
    {
        [Attribute("requiredFeatures", SvgElement.SvgNamespace)]
        public string? RequiredFeatures
        {
            get => this.GetAttribute("requiredFeatures", false, null);
            set => this.SetAttribute("requiredFeatures", value);
        }

        [Attribute("requiredExtensions", SvgElement.SvgNamespace)]
        public string? RequiredExtensions
        {
            get => this.GetAttribute("requiredExtensions", false, null);
            set => this.SetAttribute("requiredExtensions", value);
        }

        [Attribute("systemLanguage", SvgElement.SvgNamespace)]
        public string? SystemLanguage
        {
            get => this.GetAttribute("systemLanguage", false, null);
            set => this.SetAttribute("systemLanguage", value);
        }
    }
}
