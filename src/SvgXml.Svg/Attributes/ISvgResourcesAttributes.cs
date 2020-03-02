using System;
using Xml;

namespace Svg
{
    public interface ISvgResourcesAttributes : IElement
    {
        // TODO:
        // https://www.w3.org/TR/SVG11/struct.html#ExternalResourcesRequiredAttribute
        [Attribute("externalResourcesRequired", SvgElement.SvgNamespace)]
        public string? ExternalResourcesRequired
        {
            get => this.GetAttribute("externalResourcesRequired", false, "false");
            set => this.SetAttribute("externalResourcesRequired", value);
        }
    }
}
