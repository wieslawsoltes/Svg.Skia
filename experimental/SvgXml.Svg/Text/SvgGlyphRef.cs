using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
{
    [Element("glyphRef")]
    public class SvgGlyphRef : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x", false, null); // TODO:
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y", false, null); // TODO:
            set => this.SetAttribute("y", value);
        }

        [Attribute("dx", SvgNamespace)]
        public string? Dx
        {
            get => this.GetAttribute("dx", false, null);
            set => this.SetAttribute("dx", value);
        }

        [Attribute("dy", SvgNamespace)]
        public string? Dy
        {
            get => this.GetAttribute("dy", false, null); // TODO:
            set => this.SetAttribute("dy", value);
        }

        [Attribute("glyphRef", SvgNamespace)]
        public string? GlyphRef
        {
            get => this.GetAttribute("glyphRef", false, null);
            set => this.SetAttribute("glyphRef", value);
        }

        [Attribute("format", SvgNamespace)]
        public string? Format
        {
            get => this.GetAttribute("format", false, null);
            set => this.SetAttribute("format", value);
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
                case "dx":
                    Dx = value;
                    break;
                case "dy":
                    Dy = value;
                    break;
                case "glyphRef":
                    GlyphRef = value;
                    break;
                case "format":
                    Format = value;
                    break;
                case "href":
                    Href = value;
                    break;
            }
        }
    }
}
