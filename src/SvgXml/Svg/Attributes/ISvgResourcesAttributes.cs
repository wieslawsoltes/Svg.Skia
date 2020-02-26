using System;
using Xml;

namespace Svg
{
    public interface ISvgResourcesAttributes : IElement, ISvgAttributePrinter
    {
        [Attribute("externalResourcesRequired")]
        public string? ExternalResourcesRequired
        {
            get => GetAttribute("externalResourcesRequired");
            set => SetAttribute("externalResourcesRequired", value);
        }

        public void PrintResourcesAttributes(string indent)
        {
            if (ExternalResourcesRequired != null)
            {
                Console.WriteLine($"{indent}{nameof(ExternalResourcesRequired)}='{ExternalResourcesRequired}'");
            }
        }
    }
}
