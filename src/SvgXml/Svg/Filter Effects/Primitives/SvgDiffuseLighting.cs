using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feDiffuseLighting")]
    public class SvgDiffuseLighting : SvgFilterPrimitive,
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

        [Attribute("diffuseConstant", SvgNamespace)]
        public string? DiffuseConstant
        {
            get => this.GetAttribute("diffuseConstant");
            set => this.SetAttribute("diffuseConstant", value);
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
            if (DiffuseConstant != null)
            {
                write($"{indent}{nameof(DiffuseConstant)}: \"{DiffuseConstant}\"");
            }
            if (KernelUnitLength != null)
            {
                write($"{indent}{nameof(KernelUnitLength)}: \"{KernelUnitLength}\"");
            }
        }
    }
}
