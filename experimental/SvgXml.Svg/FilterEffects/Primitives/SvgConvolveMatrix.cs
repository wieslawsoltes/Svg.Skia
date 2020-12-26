using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects.Primitives
{
    [Element("feConvolveMatrix")]
    public class SvgConvolveMatrix : SvgFilterPrimitive,
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

        [Attribute("order", SvgNamespace)]
        public string? Order
        {
            get => this.GetAttribute("order", false, "3");
            set => this.SetAttribute("order", value);
        }

        [Attribute("kernelMatrix", SvgNamespace)]
        public string? KernelMatrix
        {
            get => this.GetAttribute("kernelMatrix", false, null); // TODO:
            set => this.SetAttribute("kernelMatrix", value);
        }

        [Attribute("divisor", SvgNamespace)]
        public string? Divisor
        {
            get => this.GetAttribute("divisor", false, null); // TODO:
            set => this.SetAttribute("divisor", value);
        }

        [Attribute("bias", SvgNamespace)]
        public string? Bias
        {
            get => this.GetAttribute("bias", false, "0");
            set => this.SetAttribute("bias", value);
        }

        [Attribute("targetX", SvgNamespace)]
        public string? TargetX
        {
            get => this.GetAttribute("targetX", false, null); // TODO:
            set => this.SetAttribute("targetX", value);
        }

        [Attribute("targetY", SvgNamespace)]
        public string? TargetY
        {
            get => this.GetAttribute("targetY", false, null); // TODO:
            set => this.SetAttribute("targetY", value);
        }

        [Attribute("edgeMode", SvgNamespace)]
        public string? EdgeMode
        {
            get => this.GetAttribute("edgeMode", false, "duplicate");
            set => this.SetAttribute("edgeMode", value);
        }

        [Attribute("kernelUnitLength", SvgNamespace)]
        public string? KernelUnitLength
        {
            get => this.GetAttribute("kernelUnitLength", false, "1"); // TODO:
            set => this.SetAttribute("kernelUnitLength", value);
        }

        [Attribute("preserveAlpha", SvgNamespace)]
        public string? PreserveAlpha
        {
            get => this.GetAttribute("preserveAlpha", false, "false");
            set => this.SetAttribute("preserveAlpha", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "in":
                    Input = value;
                    break;
                case "order":
                    Order = value;
                    break;
                case "kernelMatrix":
                    KernelMatrix = value;
                    break;
                case "divisor":
                    Divisor = value;
                    break;
                case "bias":
                    Bias = value;
                    break;
                case "targetX":
                    TargetX = value;
                    break;
                case "targetY":
                    TargetY = value;
                    break;
                case "edgeMode":
                    EdgeMode = value;
                    break;
                case "kernelUnitLength":
                    KernelUnitLength = value;
                    break;
                case "preserveAlpha":
                    PreserveAlpha = value;
                    break;
            }
        }
    }
}
