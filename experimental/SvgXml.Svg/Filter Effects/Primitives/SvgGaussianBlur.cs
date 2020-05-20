using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feGaussianBlur")]
    public class SvgGaussianBlur : SvgFilterPrimitive,
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

        [Attribute("stdDeviation", SvgNamespace)]
        public string? StdDeviation
        {
            get => this.GetAttribute("stdDeviation", false, "0");
            set => this.SetAttribute("stdDeviation", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "in":
                    Input = value;
                    break;
                case "stdDeviation":
                    StdDeviation = value;
                    break;
            }
        }
    }
}
