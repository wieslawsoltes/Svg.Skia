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
            get => this.GetAttribute("in", false, null);
            set => this.SetAttribute("in", value);
        }

        [Attribute("surfaceScale", SvgNamespace)]
        public string? SurfaceScale
        {
            get => this.GetAttribute("surfaceScale", false, "1");
            set => this.SetAttribute("surfaceScale", value);
        }

        [Attribute("diffuseConstant", SvgNamespace)]
        public string? DiffuseConstant
        {
            get => this.GetAttribute("diffuseConstant", false, "1");
            set => this.SetAttribute("diffuseConstant", value);
        }

        [Attribute("kernelUnitLength", SvgNamespace)]
        public string? KernelUnitLength
        {
            get => this.GetAttribute("kernelUnitLength", false, "1"); // TODO:
            set => this.SetAttribute("kernelUnitLength", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "in":
                    Input = value;
                    break;
                case "surfaceScale":
                    SurfaceScale = value;
                    break;
                case "diffuseConstant":
                    DiffuseConstant = value;
                    break;
                case "kernelUnitLength":
                    KernelUnitLength = value;
                    break;
            }
        }
    }
}
