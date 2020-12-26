using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Text
{
    [Element("font-face")]
    public class SvgFontFace : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("font-family", SvgNamespace)]
        public string? FontFamily
        {
            get => this.GetAttribute("font-family", false, null);
            set => this.SetAttribute("font-family", value);
        }

        [Attribute("font-style", SvgNamespace)]
        public string? FontStyle
        {
            get => this.GetAttribute("font-style", false, "all");
            set => this.SetAttribute("font-style", value);
        }

        [Attribute("font-variant", SvgNamespace)]
        public string? FontVariant
        {
            get => this.GetAttribute("font-variant", false, "normal");
            set => this.SetAttribute("font-variant", value);
        }

        [Attribute("font-weight", SvgNamespace)]
        public string? FontWeight
        {
            get => this.GetAttribute("font-weight", false, "all");
            set => this.SetAttribute("font-weight", value);
        }

        [Attribute("font-stretch", SvgNamespace)]
        public string? FontStretch
        {
            get => this.GetAttribute("font-stretch", false, "normal");
            set => this.SetAttribute("font-stretch", value);
        }

        [Attribute("font-size", SvgNamespace)]
        public string? FontSize
        {
            get => this.GetAttribute("font-size", false, "all");
            set => this.SetAttribute("font-size", value);
        }

        [Attribute("unicode-range", SvgNamespace)]
        public string? UnicodeRange
        {
            get => this.GetAttribute("unicode-range", false, "U+0-10FFFF");
            set => this.SetAttribute("unicode-range", value);
        }

        [Attribute("units-per-em", SvgNamespace)]
        public string? UnitsPerEm
        {
            get => this.GetAttribute("units-per-em", false, "1000");
            set => this.SetAttribute("units-per-em", value);
        }

        [Attribute("panose-1", SvgNamespace)]
        public string? Panose1
        {
            get => this.GetAttribute("panose-1", false, "0 0 0 0 0 0 0 0 0 0");
            set => this.SetAttribute("panose-1", value);
        }

        [Attribute("stemv", SvgNamespace)]
        public string? StemV
        {
            get => this.GetAttribute("stemv", false, null); // TODO:
            set => this.SetAttribute("stemv", value);
        }

        [Attribute("stemh", SvgNamespace)]
        public string? StemH
        {
            get => this.GetAttribute("stemh", false, null); // TODO:
            set => this.SetAttribute("stemh", value);
        }

        [Attribute("slope", SvgNamespace)]
        public string? Slope
        {
            get => this.GetAttribute("slope", false, "0");
            set => this.SetAttribute("slope", value);
        }

        [Attribute("cap-height", SvgNamespace)]
        public string? CapHeight
        {
            get => this.GetAttribute("cap-height", false, null); // TODO:
            set => this.SetAttribute("cap-height", value);
        }

        [Attribute("x-height", SvgNamespace)]
        public string? XHeight
        {
            get => this.GetAttribute("x-height", false, null); // TODO:
            set => this.SetAttribute("x-height", value);
        }

        [Attribute("accent-height", SvgNamespace)]
        public string? AccentHeight
        {
            get => this.GetAttribute("accent-height", false, null); // TODO:
            set => this.SetAttribute("accent-height", value);
        }

        [Attribute("ascent", SvgNamespace)]
        public string? Ascent
        {
            get => this.GetAttribute("ascent", false, null); // TODO:
            set => this.SetAttribute("ascent", value);
        }

        [Attribute("descent", SvgNamespace)]
        public string? Descent
        {
            get => this.GetAttribute("descent", false, null); // TODO:
            set => this.SetAttribute("descent", value);
        }

        [Attribute("widths", SvgNamespace)]
        public string? Widths
        {
            get => this.GetAttribute("widths", false, null); // TODO:
            set => this.SetAttribute("widths", value);
        }

        [Attribute("bbox", SvgNamespace)]
        public string? BBox
        {
            get => this.GetAttribute("bbox", false, null); // TODO:
            set => this.SetAttribute("bbox", value);
        }

        [Attribute("ideographic", SvgNamespace)]
        public string? Ideographic
        {
            get => this.GetAttribute("ideographic", false, null); // TODO:
            set => this.SetAttribute("ideographic", value);
        }

        [Attribute("alphabetic", SvgNamespace)]
        public string? Alphabetic
        {
            get => this.GetAttribute("alphabetic", false, "0");
            set => this.SetAttribute("alphabetic", value);
        }

        [Attribute("mathematical", SvgNamespace)]
        public string? Mathematical
        {
            get => this.GetAttribute("mathematical", false, null); // TODO:
            set => this.SetAttribute("mathematical", value);
        }

        [Attribute("hanging", SvgNamespace)]
        public string? Hanging
        {
            get => this.GetAttribute("hanging", false, null); // TODO:
            set => this.SetAttribute("hanging", value);
        }

        [Attribute("v-ideographic", SvgNamespace)]
        public string? VIdeographic
        {
            get => this.GetAttribute("v-ideographic", false, null); // TODO:
            set => this.SetAttribute("v-ideographic", value);
        }

        [Attribute("v-alphabetic", SvgNamespace)]
        public string? VAlphabetic
        {
            get => this.GetAttribute("v-alphabetic", false, null); // TODO:
            set => this.SetAttribute("v-alphabetic", value);
        }

        [Attribute("v-mathematical", SvgNamespace)]
        public string? VMathematical
        {
            get => this.GetAttribute("v-mathematical", false, null); // TODO:
            set => this.SetAttribute("v-mathematical", value);
        }

        [Attribute("v-hanging", SvgNamespace)]
        public string? VHanging
        {
            get => this.GetAttribute("v-hanging", false, null); // TODO:
            set => this.SetAttribute("v-hanging", value);
        }

        [Attribute("underline-position", SvgNamespace)]
        public string? UnderlinePosition
        {
            get => this.GetAttribute("underline-position", false, null); // TODO:
            set => this.SetAttribute("underline-position", value);
        }

        [Attribute("underline-thickness", SvgNamespace)]
        public string? UnderlineThickness
        {
            get => this.GetAttribute("underline-thickness", false, null); // TODO:
            set => this.SetAttribute("underline-thickness", value);
        }

        [Attribute("strikethrough-position", SvgNamespace)]
        public string? StrikethroughPosition
        {
            get => this.GetAttribute("strikethrough-position", false, null); // TODO:
            set => this.SetAttribute("strikethrough-position", value);
        }

        [Attribute("strikethrough-thickness", SvgNamespace)]
        public string? StrikethroughThickness
        {
            get => this.GetAttribute("strikethrough-thickness", false, null); // TODO:
            set => this.SetAttribute("strikethrough-thickness", value);
        }

        [Attribute("overline-position", SvgNamespace)]
        public string? OverlinePosition
        {
            get => this.GetAttribute("overline-position", false, null); // TODO:
            set => this.SetAttribute("overline-position", value);
        }

        [Attribute("overline-thickness", SvgNamespace)]
        public string? OverlineThickness
        {
            get => this.GetAttribute("overline-thickness", false, null); // TODO:
            set => this.SetAttribute("overline-thickness", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "font-family":
                    FontFamily = value;
                    break;
                case "font-style":
                    FontStyle = value;
                    break;
                case "font-variant":
                    FontVariant = value;
                    break;
                case "font-weight":
                    FontWeight = value;
                    break;
                case "font-stretch":
                    FontStretch = value;
                    break;
                case "font-size":
                    FontSize = value;
                    break;
                case "unicode-range":
                    UnicodeRange = value;
                    break;
                case "units-per-em":
                    UnitsPerEm = value;
                    break;
                case "panose-1":
                    Panose1 = value;
                    break;
                case "stemv":
                    StemV = value;
                    break;
                case "stemh":
                    StemH = value;
                    break;
                case "slope":
                    Slope = value;
                    break;
                case "cap-height":
                    CapHeight = value;
                    break;
                case "x-height":
                    XHeight = value;
                    break;
                case "accent-height":
                    AccentHeight = value;
                    break;
                case "ascent":
                    Ascent = value;
                    break;
                case "descent":
                    Descent = value;
                    break;
                case "widths":
                    Widths = value;
                    break;
                case "bbox":
                    BBox = value;
                    break;
                case "ideographic":
                    Ideographic = value;
                    break;
                case "alphabetic":
                    Alphabetic = value;
                    break;
                case "mathematical":
                    Mathematical = value;
                    break;
                case "hanging":
                    Hanging = value;
                    break;
                case "v-ideographic":
                    VIdeographic = value;
                    break;
                case "v-alphabetic":
                    VAlphabetic = value;
                    break;
                case "v-mathematical":
                    VMathematical = value;
                    break;
                case "v-hanging":
                    VHanging = value;
                    break;
                case "underline-position":
                    UnderlinePosition = value;
                    break;
                case "underline-thickness":
                    UnderlineThickness = value;
                    break;
                case "strikethrough-position":
                    StrikethroughPosition = value;
                    break;
                case "strikethrough-thickness":
                    StrikethroughThickness = value;
                    break;
                case "overline-position":
                    OverlinePosition = value;
                    break;
                case "overline-thickness":
                    OverlineThickness = value;
                    break;
            }
        }
    }
}
