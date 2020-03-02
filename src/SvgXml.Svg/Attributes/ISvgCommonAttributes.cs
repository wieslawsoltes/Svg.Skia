using System;
using Xml;

namespace Svg
{
    public interface ISvgCommonAttributes : IElement, IId
    {
        [Attribute("id", SvgElement.SvgNamespace)]
        string? IId.Id
        {
            get => this.GetAttribute("id", false, null);
            set => this.SetAttribute("id", value);
        }

        [Attribute("base", SvgElement.XmlNamespace)]
        public string? Base
        {
            get => this.GetAttribute("base", false, null);
            set => this.SetAttribute("base", value);
        }

        [Attribute("lang", SvgElement.XmlNamespace)]
        public string? Lang
        {
            get => this.GetAttribute("lang", false, null);
            set => this.SetAttribute("lang", value);
        }

        [Attribute("space", SvgElement.XmlNamespace)]
        public string? Space
        {
            get => this.GetAttribute("space", false, "default");
            set => this.SetAttribute("space", value);
        }
    }
}
