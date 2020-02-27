using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feSpecularLighting")]
    public class SvgSpecularLighting : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in", SvgElement.SvgNamespace)]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("surfaceScale", SvgElement.SvgNamespace)]
        public string? SurfaceScale
        {
            get => GetAttribute("surfaceScale");
            set => SetAttribute("surfaceScale", value);
        }

        [Attribute("specularConstant", SvgElement.SvgNamespace)]
        public string? SpecularConstant
        {
            get => GetAttribute("specularConstant");
            set => SetAttribute("specularConstant", value);
        }

        [Attribute("specularExponent", SvgElement.SvgNamespace)]
        public string? SpecularExponent
        {
            get => GetAttribute("specularExponent");
            set => SetAttribute("specularExponent", value);
        }

        [Attribute("kernelUnitLength", SvgElement.SvgNamespace)]
        public string? KernelUnitLength
        {
            get => GetAttribute("kernelUnitLength");
            set => SetAttribute("kernelUnitLength", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Input != null)
            {
                write($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (SurfaceScale != null)
            {
                write($"{indent}{nameof(SurfaceScale)}: \"{SurfaceScale}\"");
            }
            if (SpecularConstant != null)
            {
                write($"{indent}{nameof(SpecularConstant)}: \"{SpecularConstant}\"");
            }
            if (SpecularExponent != null)
            {
                write($"{indent}{nameof(SpecularExponent)}: \"{SpecularExponent}\"");
            }
            if (KernelUnitLength != null)
            {
                write($"{indent}{nameof(KernelUnitLength)}: \"{KernelUnitLength}\"");
            }
        }
    }
}
