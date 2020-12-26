using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects
{
    [Element("feSpotLight")]
    public class SvgSpotLight : SvgElement,
        ISvgCommonAttributes
    {
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

        [Attribute("z", SvgNamespace)]
        public string? Z
        {
            get => this.GetAttribute("z", false, "0");
            set => this.SetAttribute("z", value);
        }

        [Attribute("pointsAtX", SvgNamespace)]
        public string? PointsAtX
        {
            get => this.GetAttribute("pointsAtX", false, "0");
            set => this.SetAttribute("pointsAtX", value);
        }

        [Attribute("pointsAtY", SvgNamespace)]
        public string? PointsAtY
        {
            get => this.GetAttribute("pointsAtY", false, "0");
            set => this.SetAttribute("pointsAtY", value);
        }

        [Attribute("pointsAtZ", SvgNamespace)]
        public string? PointsAtZ
        {
            get => this.GetAttribute("pointsAtZ", false, "0");
            set => this.SetAttribute("pointsAtZ", value);
        }

        [Attribute("specularExponent", SvgNamespace)]
        public string? SpecularExponent
        {
            get => this.GetAttribute("specularExponent", false, "1");
            set => this.SetAttribute("specularExponent", value);
        }

        [Attribute("limitingConeAngle", SvgNamespace)]
        public string? LimitingConeAngle
        {
            get => this.GetAttribute("limitingConeAngle", false, null);
            set => this.SetAttribute("limitingConeAngle", value);
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
                case "z":
                    Z = value;
                    break;
                case "pointsAtX":
                    PointsAtX = value;
                    break;
                case "pointsAtY":
                    PointsAtY = value;
                    break;
                case "pointsAtZ":
                    PointsAtZ = value;
                    break;
                case "specularExponent":
                    SpecularExponent = value;
                    break;
                case "limitingConeAngle":
                    LimitingConeAngle = value;
                    break;
            }
        }
    }
}
