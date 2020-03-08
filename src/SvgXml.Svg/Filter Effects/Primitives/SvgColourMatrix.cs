using System;
using Xml;

namespace Svg.FilterEffects
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

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Input != null)
            {
                write($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (Type != null)
            {
                write($"{indent}{nameof(Type)}: \"{Type}\"");
            }
            if (Values != null)
            {
                write($"{indent}{nameof(Values)}: \"{Values}\"");
            }
        }
    }
}
