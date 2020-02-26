using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feBlend")]
    public class SvgBlend : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("in2")]
        public string? Input2
        {
            get => GetAttribute("in2");
            set => SetAttribute("in2", value);
        }

        [Attribute("mode")]
        public string? Mode
        {
            get => GetAttribute("mode");
            set => SetAttribute("mode", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (Input2 != null)
            {
                Console.WriteLine($"{indent}{nameof(Input2)}: \"{Input2}\"");
            }
            if (Mode != null)
            {
                Console.WriteLine($"{indent}{nameof(Mode)}: \"{Mode}\"");
            }
        }
    }
}
