using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feComposite")]
    public class SvgComposite : SvgFilterPrimitive,
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

        [Attribute("in2", SvgNamespace)]
        public string? Input2
        {
            get => this.GetAttribute("in2", false, null);
            set => this.SetAttribute("in2", value);
        }

        [Attribute("operator", SvgNamespace)]
        public string? Operator
        {
            get => this.GetAttribute("operator", false, "over");
            set => this.SetAttribute("operator", value);
        }

        [Attribute("k1", SvgNamespace)]
        public string? K1
        {
            get => this.GetAttribute("k1", false, "0");
            set => this.SetAttribute("k1", value);
        }

        [Attribute("k2", SvgNamespace)]
        public string? K2
        {
            get => this.GetAttribute("k2", false, "0");
            set => this.SetAttribute("k2", value);
        }

        [Attribute("k3", SvgNamespace)]
        public string? K3
        {
            get => this.GetAttribute("k3", false, "0");
            set => this.SetAttribute("k3", value);
        }

        [Attribute("k4", SvgNamespace)]
        public string? K4
        {
            get => this.GetAttribute("k4", false, "0");
            set => this.SetAttribute("k4", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "in":
                    Input = value;
                    break;
                case "in2":
                    Input2 = value;
                    break;
                case "operator":
                    Operator = value;
                    break;
                case "k1":
                    K1 = value;
                    break;
                case "k2":
                    K2 = value;
                    break;
                case "k3":
                    K3 = value;
                    break;
                case "k4":
                    K4 = value;
                    break;
            }
        }
    }
}
