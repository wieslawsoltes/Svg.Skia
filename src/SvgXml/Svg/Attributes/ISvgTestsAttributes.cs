using System;
using Xml;

namespace Svg
{
    public interface ISvgTestsAttributes : IElement
    {
        [Attribute("requiredFeatures", SvgAttributes.SvgNamespace)]
        public string? RequiredFeatures
        {
            get => GetAttribute("requiredFeatures");
            set => SetAttribute("requiredFeatures", value);
        }

        [Attribute("requiredExtensions", SvgAttributes.SvgNamespace)]
        public string? RequiredExtensions
        {
            get => GetAttribute("requiredExtensions");
            set => SetAttribute("requiredExtensions", value);
        }

        [Attribute("systemLanguage", SvgAttributes.SvgNamespace)]
        public string? SystemLanguage
        {
            get => GetAttribute("systemLanguage");
            set => SetAttribute("systemLanguage", value);
        }
    }
}
