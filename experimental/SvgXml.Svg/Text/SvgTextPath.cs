using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Text
{
    [Element("textPath")]
    public class SvgTextPath : SvgTextContent,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("href", XLinkNamespace)]
        public override string? Href
        {
            get => this.GetAttribute("href", false, null);
            set => this.SetAttribute("href", value);
        }

        [Attribute("startOffset", SvgNamespace)]
        public string? StartOffset
        {
            get => this.GetAttribute("startOffset", false, "0");
            set => this.SetAttribute("startOffset", value);
        }

        [Attribute("method", SvgNamespace)]
        public string? Method
        {
            get => this.GetAttribute("method", false, "align");
            set => this.SetAttribute("method", value);
        }

        [Attribute("spacing", SvgNamespace)]
        public string? Spacing
        {
            get => this.GetAttribute("spacing", false, "exact");
            set => this.SetAttribute("spacing", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "href":
                    Href = value;
                    break;
                case "startOffset":
                    StartOffset = value;
                    break;
                case "method":
                    Method = value;
                    break;
                case "spacing":
                    Spacing = value;
                    break;
            }
        }
    }
}
