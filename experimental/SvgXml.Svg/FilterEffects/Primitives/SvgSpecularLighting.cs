using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects.Primitives
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
            get => this.GetAttribute("in", false, null);
            set => this.SetAttribute("in", value);
        }

        [Attribute("surfaceScale", SvgNamespace)]
        public string? SurfaceScale
        {
            get => this.GetAttribute("surfaceScale", false, "1");
            set => this.SetAttribute("surfaceScale", value);
        }

        [Attribute("specularConstant", SvgNamespace)]
        public string? SpecularConstant
        {
            get => this.GetAttribute("specularConstant", false, "1");
            set => this.SetAttribute("specularConstant", value);
        }

        [Attribute("specularExponent", SvgNamespace)]
        public string? SpecularExponent
        {
            get => this.GetAttribute("specularExponent", false, "1");
            set => this.SetAttribute("specularExponent", value);
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
                case "specularConstant":
                    SpecularConstant = value;
                    break;
                case "specularExponent":
                    SpecularExponent = value;
                    break;
                case "kernelUnitLength":
                    KernelUnitLength = value;
                    break;
            }
        }
    }
}
