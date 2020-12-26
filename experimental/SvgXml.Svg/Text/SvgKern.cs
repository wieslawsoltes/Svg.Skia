using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
{
    public abstract class SvgKern : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("u1", SvgNamespace)]
        public string? Unicode1
        {
            get => this.GetAttribute("u1", false, null);
            set => this.SetAttribute("u1", value);
        }

        [Attribute("g1", SvgNamespace)]
        public string? Glyph1
        {
            get => this.GetAttribute("g1", false, null);
            set => this.SetAttribute("g1", value);
        }

        [Attribute("u2", SvgNamespace)]
        public string? Unicode2
        {
            get => this.GetAttribute("u2", false, null);
            set => this.SetAttribute("u2", value);
        }

        [Attribute("g2", SvgNamespace)]
        public string? Glyph2
        {
            get => this.GetAttribute("g2", false, null);
            set => this.SetAttribute("g2", value);
        }

        [Attribute("k", SvgNamespace)]
        public string? Kerning
        {
            get => this.GetAttribute("k", false, null);
            set => this.SetAttribute("k", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "u1":
                    Unicode1 = value;
                    break;
                case "g1":
                    Glyph1 = value;
                    break;
                case "u2":
                    Unicode2 = value;
                    break;
                case "g2":
                    Glyph2 = value;
                    break;
                case "k":
                    Kerning = value;
                    break;
            }
        }
    }
}
