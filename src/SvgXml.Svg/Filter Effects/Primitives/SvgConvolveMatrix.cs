using System;
using Xml;

namespace Svg.FilterEffects
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

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Input != null)
            {
                write($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (Order != null)
            {
                write($"{indent}{nameof(Order)}: \"{Order}\"");
            }
            if (KernelMatrix != null)
            {
                write($"{indent}{nameof(KernelMatrix)}: \"{KernelMatrix}\"");
            }
            if (Divisor != null)
            {
                write($"{indent}{nameof(Divisor)}: \"{Divisor}\"");
            }
            if (Bias != null)
            {
                write($"{indent}{nameof(Bias)}: \"{Bias}\"");
            }
            if (TargetX != null)
            {
                write($"{indent}{nameof(TargetX)}: \"{TargetX}\"");
            }
            if (TargetY != null)
            {
                write($"{indent}{nameof(TargetY)}: \"{TargetY}\"");
            }
            if (EdgeMode != null)
            {
                write($"{indent}{nameof(EdgeMode)}: \"{EdgeMode}\"");
            }
            if (KernelUnitLength != null)
            {
                write($"{indent}{nameof(KernelUnitLength)}: \"{KernelUnitLength}\"");
            }
            if (PreserveAlpha != null)
            {
                write($"{indent}{nameof(PreserveAlpha)}: \"{PreserveAlpha}\"");
            }
        }
    }
}
