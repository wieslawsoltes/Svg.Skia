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
            get => this.GetAttribute("in");
            set => this.SetAttribute("in", value);
        }

        [Attribute("type", SvgNamespace)]
        public string? Type
        {
            get => this.GetAttribute("type");
            set => this.SetAttribute("type", value);
        }

        [Attribute("values", SvgNamespace)]
        public string? Values
        {
            get => this.GetAttribute("values");
            set => this.SetAttribute("values", value);
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
