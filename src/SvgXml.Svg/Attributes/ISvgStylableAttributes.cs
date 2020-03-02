using System;
using Xml;

namespace Svg
{
    public interface ISvgStylableAttributes : IElement
    {
        [Attribute("class", SvgElement.SvgNamespace)]
        public string? Class
        {
            get => this.GetAttribute("class", false, null);
            set => this.SetAttribute("class", value);
        }

        [Attribute("style", SvgElement.SvgNamespace)]
        public string? Style
        {
            get => this.GetAttribute("style", false, null);
            set => this.SetAttribute("style", value);
        }
    }
}
