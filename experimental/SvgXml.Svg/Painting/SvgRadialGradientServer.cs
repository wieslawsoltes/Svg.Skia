using System;
using Xml;

namespace Svg
{
    [Element("radialGradient")]
    public class SvgRadialGradientServer : SvgGradientServer,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("cx", SvgNamespace)]
        public string? CenterX
        {
            get => this.GetAttribute("cx", false, "50%");
            set => this.SetAttribute("cx", value);
        }

        [Attribute("cy", SvgNamespace)]
        public string? CenterY
        {
            get => this.GetAttribute("cy", false, "50%");
            set => this.SetAttribute("cy", value);
        }

        [Attribute("r", SvgNamespace)]
        public string? Radius
        {
            get => this.GetAttribute("r", false, "50%");
            set => this.SetAttribute("r", value);
        }

        [Attribute("fx", SvgNamespace)]
        public string? FocalX
        {
            get => this.GetAttribute("fx", false, null);
            set => this.SetAttribute("fx", value);
        }

        [Attribute("fy", SvgNamespace)]
        public string? FocalY
        {
            get => this.GetAttribute("fy", false, null);
            set => this.SetAttribute("fy", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "cx":
                    CenterX = value;
                    break;
                case "cy":
                    CenterY = value;
                    break;
                case "r":
                    Radius = value;
                    break;
                case "fx":
                    FocalX = value;
                    break;
                case "fy":
                    FocalY = value;
                    break;
            }
        }
    }
}
