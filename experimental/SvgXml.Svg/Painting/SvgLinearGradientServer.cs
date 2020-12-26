using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Painting
{
    [Element("linearGradient")]
    public class SvgLinearGradientServer : SvgGradientServer,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("x1", SvgNamespace)]
        public string? X1
        {
            get => this.GetAttribute("x1", false, "0%");
            set => this.SetAttribute("x1", value);
        }

        [Attribute("y1", SvgNamespace)]
        public string? Y1
        {
            get => this.GetAttribute("y1", false, "0%");
            set => this.SetAttribute("y1", value);
        }

        [Attribute("x2", SvgNamespace)]
        public string? X2
        {
            get => this.GetAttribute("x2", false, "100%");
            set => this.SetAttribute("x2", value);
        }

        [Attribute("y2", SvgNamespace)]
        public string? Y2
        {
            get => this.GetAttribute("y2", false, "100%");
            set => this.SetAttribute("y2", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "x1":
                    X1 = value;
                    break;
                case "y1":
                    Y1 = value;
                    break;
                case "x2":
                    X2 = value;
                    break;
                case "y2":
                    Y2 = value;
                    break;
            }
        }
    }
}
