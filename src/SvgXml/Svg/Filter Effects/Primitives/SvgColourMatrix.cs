using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feColorMatrix")]
    public class SvgColourMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("type")]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        [Attribute("values")]
        public string? Values
        {
            get => GetAttribute("values");
            set => SetAttribute("values", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (Type != null)
            {
                Console.WriteLine($"{indent}{nameof(Type)}: \"{Type}\"");
            }
            if (Values != null)
            {
                Console.WriteLine($"{indent}{nameof(Values)}: \"{Values}\"");
            }
        }
    }
}
