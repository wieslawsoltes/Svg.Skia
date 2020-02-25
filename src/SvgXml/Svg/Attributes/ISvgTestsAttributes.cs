using System;
using Xml;

namespace Svg
{
    public interface ISvgTestsAttributes : IElement, ISvgAttributePrinter
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

        public void PrintTestsAttributes(string indent)
        {
            if (RequiredFeatures != null)
            {
                Console.WriteLine($"{indent}{nameof(RequiredFeatures)}='{RequiredFeatures}'");
            }
            if (RequiredExtensions != null)
            {
                Console.WriteLine($"{indent}{nameof(RequiredExtensions)}='{RequiredExtensions}'");
            }
            if (SystemLanguage != null)
            {
                Console.WriteLine($"{indent}{nameof(SystemLanguage)}='{SystemLanguage}'");
            }
        }
    }
}
