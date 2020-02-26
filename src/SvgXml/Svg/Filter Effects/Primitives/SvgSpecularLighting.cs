using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feSpecularLighting")]
    public class SvgSpecularLighting : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("surfaceScale")]
        public string? SurfaceScale
        {
            get => GetAttribute("surfaceScale");
            set => SetAttribute("surfaceScale", value);
        }

        [Attribute("specularConstant")]
        public string? SpecularConstant
        {
            get => GetAttribute("specularConstant");
            set => SetAttribute("specularConstant", value);
        }

        [Attribute("specularExponent")]
        public string? SpecularExponent
        {
            get => GetAttribute("specularExponent");
            set => SetAttribute("specularExponent", value);
        }

        [Attribute("kernelUnitLength")]
        public string? KernelUnitLength
        {
            get => GetAttribute("kernelUnitLength");
            set => SetAttribute("kernelUnitLength", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}='{Input}'");
            }
            if (SurfaceScale != null)
            {
                Console.WriteLine($"{indent}{nameof(SurfaceScale)}='{SurfaceScale}'");
            }
            if (SpecularConstant != null)
            {
                Console.WriteLine($"{indent}{nameof(SpecularConstant)}='{SpecularConstant}'");
            }
            if (SpecularExponent != null)
            {
                Console.WriteLine($"{indent}{nameof(SpecularExponent)}='{SpecularExponent}'");
            }
            if (KernelUnitLength != null)
            {
                Console.WriteLine($"{indent}{nameof(KernelUnitLength)}='{KernelUnitLength}'");
            }
        }
    }
}
