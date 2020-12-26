using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feOffset")]
    public class SvgOffset : SvgFilterPrimitive,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
        [Attribute("in", SvgNamespace)]
        public string? Input
        {
            get => this.GetAttribute("in", false, null);
            set => this.SetAttribute("in", value);
        }

        [Attribute("dx", SvgNamespace)]
        public string? Dx
        {
            get => this.GetAttribute("dx", false, "0");
            set => this.SetAttribute("dx", value);
        }

        [Attribute("dy", SvgNamespace)]
        public string? Dy
        {
            get => this.GetAttribute("dy", false, "0");
            set => this.SetAttribute("dy", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "in":
                    Input = value;
                    break;
                case "dx":
                    Dx = value;
                    break;
                case "dy":
                    Dy = value;
                    break;
            }
        }
    }
}
