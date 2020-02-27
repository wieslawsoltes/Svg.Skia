using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feColorMatrix")]
    public class SvgColourMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in", SvgNamespace)]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("type", SvgNamespace)]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        [Attribute("values", SvgNamespace)]
        public string? Values
        {
            get => GetAttribute("values");
            set => SetAttribute("values", value);
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
