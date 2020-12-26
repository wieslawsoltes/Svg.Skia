using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
{
    [Element("cursor")]
    public class SvgCursor : SvgElement,
        ISvgCommonAttributes,
        ISvgTestsAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x", false, "0");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y", false, "0");
            set => this.SetAttribute("y", value);
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
                case "x":
                    X = value;
                    break;
                case "y":
                    Y = value;
                    break;
                case "href":
                    Href = value;
                    break;
            }
        }
    }
}
