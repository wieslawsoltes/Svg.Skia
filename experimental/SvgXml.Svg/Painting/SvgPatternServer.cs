using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Painting
{
    [Element("pattern")]
    public class SvgPatternServer : SvgPaintServer,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("viewBox", SvgNamespace)]
        public string? ViewBox
        {
            get => this.GetAttribute("viewBox", false, null);
            set => this.SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio", SvgNamespace)]
        public string? AspectRatio
        {
            get => this.GetAttribute("preserveAspectRatio", false, "xMidYMid meet");
            set => this.SetAttribute("preserveAspectRatio", value);
        }

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

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => this.GetAttribute("width", false, "0");
            set => this.SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => this.GetAttribute("height", false, "0");
            set => this.SetAttribute("height", value);
        }

        [Attribute("patternUnits", SvgNamespace)]
        public string? PatternUnits
        {
            get => this.GetAttribute("patternUnits", false, "objectBoundingBox");
            set => this.SetAttribute("patternUnits", value);
        }

        [Attribute("patternContentUnits", SvgNamespace)]
        public string? PatternContentUnits
        {
            get => this.GetAttribute("patternContentUnits", false, "userSpaceOnUse");
            set => this.SetAttribute("patternContentUnits", value);
        }

        [Attribute("patternTransform", SvgNamespace)]
        public string? PatternTransform
        {
            get => this.GetAttribute("patternTransform", false, null);
            set => this.SetAttribute("patternTransform", value);
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
                case "viewBox":
                    ViewBox = value;
                    break;
                case "preserveAspectRatio":
                    AspectRatio = value;
                    break;
                case "x":
                    X = value;
                    break;
                case "y":
                    Y = value;
                    break;
                case "width":
                    Width = value;
                    break;
                case "height":
                    Height = value;
                    break;
                case "patternUnits":
                    PatternUnits = value;
                    break;
                case "patternContentUnits":
                    PatternContentUnits = value;
                    break;
                case "patternTransform":
                    PatternTransform = value;
                    break;
                case "href":
                    Href = value;
                    break;
            }
        }
    }
}
