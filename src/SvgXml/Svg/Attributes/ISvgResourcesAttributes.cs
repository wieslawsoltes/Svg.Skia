using System;
using Xml;

namespace Svg
{
    public interface ISvgResourcesAttributes : IElement
    {
        [Attribute("externalResourcesRequired")]
        public string? ExternalResourcesRequired
        {
            get => GetAttribute("externalResourcesRequired");
            set => SetAttribute("externalResourcesRequired", value);
        }
    }
}
