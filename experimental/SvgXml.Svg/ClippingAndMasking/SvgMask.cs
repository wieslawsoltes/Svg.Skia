using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.ClippingAndMasking
{
    [Element("mask")]
    public class SvgMask : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
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

        [Attribute("maskUnits", SvgNamespace)]
        public string? MaskUnits
        {
            get => this.GetAttribute("maskUnits", false, "objectBoundingBox");
            set => this.SetAttribute("maskUnits", value);
        }

        [Attribute("maskContentUnits", SvgNamespace)]
        public string? MaskContentUnits
        {
            get => this.GetAttribute("maskContentUnits", false, "userSpaceOnUse");
            set => this.SetAttribute("maskContentUnits", value);
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
                case "maskUnits":
                    MaskUnits = value;
                    break;
                case "maskContentUnits":
                    MaskContentUnits = value;
                    break;
            }
        }
    }
}
