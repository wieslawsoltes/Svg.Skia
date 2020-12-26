using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects.Primitives
{
    [Element("feBlend")]
    public class SvgBlend : SvgFilterPrimitive,
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

        [Attribute("in2", SvgNamespace)]
        public string? Input2
        {
            get => this.GetAttribute("in2", false, null);
            set => this.SetAttribute("in2", value);
        }

        [Attribute("mode", SvgNamespace)]
        public string? Mode
        {
            get => this.GetAttribute("mode", false, "normal");
            set => this.SetAttribute("mode", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "in":
                    Input = value;
                    break;
                case "in2":
                    Input2 = value;
                    break;
                case "mode":
                    Mode = value;
                    break;
            }
        }
    }
}
