using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Text
{
    [Element("glyph")]
    public class SvgGlyph : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
        [Attribute("d", SvgNamespace)]
        public string? PathData
        {
            get => this.GetAttribute("d", false, null);
            set => this.SetAttribute("d", value);
        }

        [Attribute("horiz-adv-x", SvgNamespace)]
        public string? HorizAdvX
        {
            get => this.GetAttribute("horiz-adv-x", false, null); // TODO
            set => this.SetAttribute("horiz-adv-x", value);
        }

        [Attribute("vert-origin-x", SvgNamespace)]
        public string? VertOriginX
        {
            get => this.GetAttribute("vert-origin-x", false, null); // TODO
            set => this.SetAttribute("vert-origin-x", value);
        }

        [Attribute("vert-origin-y", SvgNamespace)]
        public string? VertOriginY
        {
            get => this.GetAttribute("vert-origin-y", false, null); // TODO
            set => this.SetAttribute("vert-origin-y", value);
        }

        [Attribute("vert-adv-y", SvgNamespace)]
        public string? VertAdvY
        {
            get => this.GetAttribute("vert-adv-y", false, null); // TODO
            set => this.SetAttribute("vert-adv-y", value);
        }

        [Attribute("unicode", SvgNamespace)]
        public string? Unicode
        {
            get => this.GetAttribute("unicode", false, null);
            set => this.SetAttribute("unicode", value);
        }

        [Attribute("glyph-name", SvgNamespace)]
        public string? GlyphName
        {
            get => this.GetAttribute("glyph-name", false, null);
            set => this.SetAttribute("glyph-name", value);
        }

        [Attribute("orientation", SvgNamespace)]
        public string? Orientation
        {
            get => this.GetAttribute("orientation", false, null);
            set => this.SetAttribute("orientation", value);
        }

        [Attribute("arabic-form", SvgNamespace)]
        public string? ArabicForm
        {
            get => this.GetAttribute("arabic-form", false, null);
            set => this.SetAttribute("arabic-form", value);
        }

        [Attribute("lang", SvgNamespace)]
        public string? LanguageCodes
        {
            get => this.GetAttribute("lang", false, null);
            set => this.SetAttribute("lang", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "d":
                    PathData = value;
                    break;
                case "horiz-adv-x":
                    HorizAdvX = value;
                    break;
                case "vert-origin-x":
                    VertOriginX = value;
                    break;
                case "vert-origin-y":
                    VertOriginY = value;
                    break;
                case "vert-adv-y":
                    VertAdvY = value;
                    break;
                case "unicode":
                    Unicode = value;
                    break;
                case "glyph-name":
                    GlyphName = value;
                    break;
                case "orientation":
                    Orientation = value;
                    break;
                case "arabic-form":
                    ArabicForm = value;
                    break;
                case "lang":
                    LanguageCodes = value;
                    break;
            }
        }
    }
}
