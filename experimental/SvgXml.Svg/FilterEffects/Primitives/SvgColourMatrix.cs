using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects.Primitives
{
    [Element("feColorMatrix")]
    public class SvgColourMatrix : SvgFilterPrimitive,
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

        [Attribute("type", SvgNamespace)]
        public override string? Type
        {
            get => this.GetAttribute("type", false, "matrix");
            set => this.SetAttribute("type", value);
        }

        [Attribute("values", SvgNamespace)]
        public string? Values
        {
            get => this.GetAttribute("values", false, null); // TODO:
            set => this.SetAttribute("values", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "in":
                    Input = value;
                    break;
                case "type":
                    Type = value;
                    break;
                case "values":
                    Values = value;
                    break;
            }
        }
    }
}
