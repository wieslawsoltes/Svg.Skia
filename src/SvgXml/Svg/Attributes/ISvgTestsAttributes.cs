using System;
using Xml;

namespace Svg
{
    public interface ISvgTestsAttributes : IElement
    {
        [Attribute("requiredFeatures")]
        public string? RequiredFeatures
        {
            get => GetAttribute("requiredFeatures");
            set => SetAttribute("requiredFeatures", value);
        }

        [Attribute("requiredExtensions")]
        public string? RequiredExtensions
        {
            get => GetAttribute("requiredExtensions");
            set => SetAttribute("requiredExtensions", value);
        }

        [Attribute("systemLanguage")]
        public string? SystemLanguage
        {
            get => GetAttribute("systemLanguage");
            set => SetAttribute("systemLanguage", value);
        }
    }
}
