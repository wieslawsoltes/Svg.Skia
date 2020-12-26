using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
{
    public abstract class SvgGradientServer : SvgPaintServer
    {
        [Attribute("gradientUnits", SvgNamespace)]
        public string? GradientUnits
        {
            get => this.GetAttribute("gradientUnits", false, "objectBoundingBox");
            set => this.SetAttribute("gradientUnits", value);
        }

        [Attribute("gradientTransform", SvgNamespace)]
        public string? GradientTransform
        {
            get => this.GetAttribute("gradientTransform", false, null);
            set => this.SetAttribute("gradientTransform", value);
        }

        [Attribute("spreadMethod", SvgNamespace)]
        public string? SpreadMethod
        {
            get => this.GetAttribute("spreadMethod", false, "pad");
            set => this.SetAttribute("spreadMethod", value);
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
                case "gradientUnits":
                    GradientUnits = value;
                    break;
                case "gradientTransform":
                    GradientTransform = value;
                    break;
                case "spreadMethod":
                    SpreadMethod = value;
                    break;
                case "href":
                    Href = value;
                    break;
            }
        }
    }
}
