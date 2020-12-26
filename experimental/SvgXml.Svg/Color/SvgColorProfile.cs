using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Color
{
    [Element("color-profile")]
    public class SvgColorProfile : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("local", SvgNamespace)]
        public string? Local
        {
            get => this.GetAttribute("local", false, null);
            set => this.SetAttribute("local", value);
        }

        [Attribute("name", SvgNamespace)]
        public string? Name
        {
            get => this.GetAttribute("name", false, null);
            set => this.SetAttribute("name", value);
        }

        [Attribute("rendering-intent", SvgNamespace)]
        public string? RenderingIntent
        {
            get => this.GetAttribute("rendering-intent", false, "auto");
            set => this.SetAttribute("rendering-intent", value);
        }

        [Attribute("href", XLinkNamespace)]
        public override string? Href
        {
            get => this.GetAttribute("href", false, null);
            set => this.SetAttribute("href", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "local":
                    Local = value;
                    break;
                case "name":
                    Name = value;
                    break;
                case "rendering-intent":
                    RenderingIntent = value;
                    break;
                case "href":
                    Href = value;
                    break;
            }
        }
    }
}
