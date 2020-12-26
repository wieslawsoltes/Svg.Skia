using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Linking
{
    [Element("a")]
    public class SvgAnchor : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
        // ISvgTransformableAttributes

        [Attribute("transform", SvgNamespace)]
        public string? Transform
        {
            get => this.GetAttribute("transform", false, null);
            set => this.SetAttribute("transform", value);
        }

        // SvgAnchor

        [Attribute("href", XLinkNamespace)]
        public override string? Href
        {
            get => this.GetAttribute("href", false, null);
            set => this.SetAttribute("href", value);
        }

        [Attribute("show", XLinkNamespace)]
        public override string? Show
        {
            get => this.GetAttribute("show", false, null); // TODO:
            set => this.SetAttribute("show", value);
        }

        [Attribute("actuate", XLinkNamespace)]
        public override string? Actuate
        {
            get => this.GetAttribute("actuate", false, null); // TODO:
            set => this.SetAttribute("actuate", value);
        }

        [Attribute("target", SvgNamespace)]
        public string? Target
        {
            get => this.GetAttribute("target", false, "_self"); // TODO:
            set => this.SetAttribute("target", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                // ISvgTransformableAttributes
                case "transform":
                    Transform = value;
                    break;
                // SvgAnchor
                case "href":
                    Href = value;
                    break;
                case "show":
                    Show = value;
                    break;
                case "actuate":
                    Actuate = value;
                    break;
                case "target":
                    Target = value;
                    break;
            }
        }
    }
}
