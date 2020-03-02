using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feSpecularLighting")]
    public class SvgSpecularLighting : SvgFilterPrimitive,
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

        [Attribute("surfaceScale", SvgNamespace)]
        public string? SurfaceScale
        {
            get => this.GetAttribute("surfaceScale");
            set => this.SetAttribute("surfaceScale", value);
        }

        [Attribute("specularConstant", SvgNamespace)]
        public string? SpecularConstant
        {
            get => this.GetAttribute("specularConstant");
            set => this.SetAttribute("specularConstant", value);
        }

        [Attribute("specularExponent", SvgNamespace)]
        public string? SpecularExponent
        {
            get => this.GetAttribute("specularExponent");
            set => this.SetAttribute("specularExponent", value);
        }

        [Attribute("kernelUnitLength", SvgNamespace)]
        public string? KernelUnitLength
        {
            get => this.GetAttribute("kernelUnitLength");
            set => this.SetAttribute("kernelUnitLength", value);
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
