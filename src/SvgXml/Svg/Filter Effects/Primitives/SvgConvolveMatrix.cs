using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feConvolveMatrix")]
    public class SvgConvolveMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("order")]
        public string? Order
        {
            get => GetAttribute("order");
            set => SetAttribute("order", value);
        }

        [Attribute("kernelMatrix")]
        public string? KernelMatrix
        {
            get => GetAttribute("kernelMatrix");
            set => SetAttribute("kernelMatrix", value);
        }

        [Attribute("divisor")]
        public string? Divisor
        {
            get => GetAttribute("divisor");
            set => SetAttribute("divisor", value);
        }

        [Attribute("bias")]
        public string? Bias
        {
            get => GetAttribute("bias");
            set => SetAttribute("bias", value);
        }

        [Attribute("targetX")]
        public string? TargetX
        {
            get => GetAttribute("targetX");
            set => SetAttribute("targetX", value);
        }

        [Attribute("targetY")]
        public string? TargetY
        {
            get => GetAttribute("targetY");
            set => SetAttribute("targetY", value);
        }

        [Attribute("edgeMode")]
        public string? EdgeMode
        {
            get => GetAttribute("edgeMode");
            set => SetAttribute("edgeMode", value);
        }

        [Attribute("kernelUnitLength")]
        public string? KernelUnitLength
        {
            get => GetAttribute("kernelUnitLength");
            set => SetAttribute("kernelUnitLength", value);
        }

        [Attribute("preserveAlpha")]
        public string? PreserveAlpha
        {
            get => GetAttribute("preserveAlpha");
            set => SetAttribute("preserveAlpha", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}='{Input}'");
            }
            if (Order != null)
            {
                Console.WriteLine($"{indent}{nameof(Order)}='{Order}'");
            }
            if (KernelMatrix != null)
            {
                Console.WriteLine($"{indent}{nameof(KernelMatrix)}='{KernelMatrix}'");
            }
            if (Divisor != null)
            {
                Console.WriteLine($"{indent}{nameof(Divisor)}='{Divisor}'");
            }
            if (Bias != null)
            {
                Console.WriteLine($"{indent}{nameof(Bias)}='{Bias}'");
            }
            if (TargetX != null)
            {
                Console.WriteLine($"{indent}{nameof(TargetX)}='{TargetX}'");
            }
            if (TargetY != null)
            {
                Console.WriteLine($"{indent}{nameof(TargetY)}='{TargetY}'");
            }
            if (EdgeMode != null)
            {
                Console.WriteLine($"{indent}{nameof(EdgeMode)}='{EdgeMode}'");
            }
            if (KernelUnitLength != null)
            {
                Console.WriteLine($"{indent}{nameof(KernelUnitLength)}='{KernelUnitLength}'");
            }
            if (PreserveAlpha != null)
            {
                Console.WriteLine($"{indent}{nameof(PreserveAlpha)}='{PreserveAlpha}'");
            }
        }
    }
}
