using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feDiffuseLighting")]
    public class SvgDiffuseLighting : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
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

        [Attribute("diffuseConstant")]
        public string? DiffuseConstant
        {
            get => GetAttribute("diffuseConstant");
            set => SetAttribute("diffuseConstant", value);
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
            if (DiffuseConstant != null)
            {
                Console.WriteLine($"{indent}{nameof(DiffuseConstant)}='{DiffuseConstant}'");
            }
            if (KernelUnitLength != null)
            {
                Console.WriteLine($"{indent}{nameof(KernelUnitLength)}='{KernelUnitLength}'");
            }
        }
    }
}
