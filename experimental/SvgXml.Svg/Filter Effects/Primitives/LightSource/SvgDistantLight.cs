using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects
{
    [Element("feDistantLight")]
    public class SvgDistantLight : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("azimuth", SvgNamespace)]
        public string? Azimuth
        {
            get => this.GetAttribute("azimuth", false, "0");
            set => this.SetAttribute("azimuth", value);
        }

        [Attribute("elevation", SvgNamespace)]
        public string? Elevation
        {
            get => this.GetAttribute("elevation", false, "0");
            set => this.SetAttribute("elevation", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "azimuth":
                    Azimuth = value;
                    break;
                case "elevation":
                    Elevation = value;
                    break;
            }
        }
    }
}
