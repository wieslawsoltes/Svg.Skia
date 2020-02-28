using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feBlend")]
    public class SvgBlend : SvgFilterPrimitive,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
        [Attribute("in", SvgNamespace)]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("in2", SvgNamespace)]
        public string? Input2
        {
            get => GetAttribute("in2");
            set => SetAttribute("in2", value);
        }

        [Attribute("mode", SvgNamespace)]
        public string? Mode
        {
            get => GetAttribute("mode");
            set => SetAttribute("mode", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Input != null)
            {
                write($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (Input2 != null)
            {
                write($"{indent}{nameof(Input2)}: \"{Input2}\"");
            }
            if (Mode != null)
            {
                write($"{indent}{nameof(Mode)}: \"{Mode}\"");
            }
        }
    }
}
