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
            get => this.GetAttribute("in");
            set => this.SetAttribute("in", value);
        }

        [Attribute("order", SvgNamespace)]
        public string? Order
        {
            get => this.GetAttribute("order");
            set => this.SetAttribute("order", value);
        }

        [Attribute("kernelMatrix", SvgNamespace)]
        public string? KernelMatrix
        {
            get => this.GetAttribute("kernelMatrix");
            set => this.SetAttribute("kernelMatrix", value);
        }

        [Attribute("divisor", SvgNamespace)]
        public string? Divisor
        {
            get => this.GetAttribute("divisor");
            set => this.SetAttribute("divisor", value);
        }

        [Attribute("bias", SvgNamespace)]
        public string? Bias
        {
            get => this.GetAttribute("bias");
            set => this.SetAttribute("bias", value);
        }

        [Attribute("targetX", SvgNamespace)]
        public string? TargetX
        {
            get => this.GetAttribute("targetX");
            set => this.SetAttribute("targetX", value);
        }

        [Attribute("targetY", SvgNamespace)]
        public string? TargetY
        {
            get => this.GetAttribute("targetY");
            set => this.SetAttribute("targetY", value);
        }

        [Attribute("edgeMode", SvgNamespace)]
        public string? EdgeMode
        {
            get => this.GetAttribute("edgeMode");
            set => this.SetAttribute("edgeMode", value);
        }

        [Attribute("kernelUnitLength", SvgNamespace)]
        public string? KernelUnitLength
        {
            get => this.GetAttribute("kernelUnitLength");
            set => this.SetAttribute("kernelUnitLength", value);
        }

        [Attribute("preserveAlpha", SvgNamespace)]
        public string? PreserveAlpha
        {
            get => this.GetAttribute("preserveAlpha");
            set => this.SetAttribute("preserveAlpha", value);
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
