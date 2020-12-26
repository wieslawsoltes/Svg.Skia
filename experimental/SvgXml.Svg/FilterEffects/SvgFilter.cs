using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects
{
    [Element("filter")]
    public class SvgFilter : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x", false, "-10%");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y", false, "-10%");
            set => this.SetAttribute("y", value);
        }

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => this.GetAttribute("width", false, "120%");
            set => this.SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => this.GetAttribute("height", false, "120%");
            set => this.SetAttribute("height", value);
        }

        [Attribute("filterRes", SvgNamespace)]
        public string? FilterRes
        {
            get => this.GetAttribute("filterRes", false, null);
            set => this.SetAttribute("filterRes", value);
        }

        [Attribute("filterUnits", SvgNamespace)]
        public string? FilterUnits
        {
            get => this.GetAttribute("filterUnits", false, "objectBoundingBox");
            set => this.SetAttribute("filterUnits", value);
        }

        [Attribute("primitiveUnits", SvgNamespace)]
        public string? PrimitiveUnits
        {
            get => this.GetAttribute("primitiveUnits", false, "userSpaceOnUse");
            set => this.SetAttribute("primitiveUnits", value);
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
                case "width":
                    Width = value;
                    break;
                case "height":
                    Height = value;
                    break;
                case "filterRes":
                    FilterRes = value;
                    break;
                case "filterUnits":
                    FilterUnits = value;
                    break;
                case "primitiveUnits":
                    PrimitiveUnits = value;
                    break;
                case "href":
                    Href = value;
                    break;
            }
        }
    }
}
