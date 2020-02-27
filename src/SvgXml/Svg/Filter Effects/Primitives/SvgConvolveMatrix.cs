using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feConvolveMatrix")]
    public class SvgConvolveMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in", SvgNamespace)]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("order", SvgNamespace)]
        public string? Order
        {
            get => GetAttribute("order");
            set => SetAttribute("order", value);
        }

        [Attribute("kernelMatrix", SvgNamespace)]
        public string? KernelMatrix
        {
            get => GetAttribute("kernelMatrix");
            set => SetAttribute("kernelMatrix", value);
        }

        [Attribute("divisor", SvgNamespace)]
        public string? Divisor
        {
            get => GetAttribute("divisor");
            set => SetAttribute("divisor", value);
        }

        [Attribute("bias", SvgNamespace)]
        public string? Bias
        {
            get => GetAttribute("bias");
            set => SetAttribute("bias", value);
        }

        [Attribute("targetX", SvgNamespace)]
        public string? TargetX
        {
            get => GetAttribute("targetX");
            set => SetAttribute("targetX", value);
        }

        [Attribute("targetY", SvgNamespace)]
        public string? TargetY
        {
            get => GetAttribute("targetY");
            set => SetAttribute("targetY", value);
        }

        [Attribute("edgeMode", SvgNamespace)]
        public string? EdgeMode
        {
            get => GetAttribute("edgeMode");
            set => SetAttribute("edgeMode", value);
        }

        [Attribute("kernelUnitLength", SvgNamespace)]
        public string? KernelUnitLength
        {
            get => GetAttribute("kernelUnitLength");
            set => SetAttribute("kernelUnitLength", value);
        }

        [Attribute("preserveAlpha", SvgNamespace)]
        public string? PreserveAlpha
        {
            get => GetAttribute("preserveAlpha");
            set => SetAttribute("preserveAlpha", value);
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
