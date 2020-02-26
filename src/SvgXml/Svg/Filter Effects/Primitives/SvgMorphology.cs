using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feMorphology")]
    public class SvgMorphology : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in", SvgAttributes.SvgNamespace)]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("operator", SvgAttributes.SvgNamespace)]
        public string? Operator
        {
            get => GetAttribute("operator");
            set => SetAttribute("operator", value);
        }

        [Attribute("radius", SvgAttributes.SvgNamespace)]
        public string? Radius
        {
            get => GetAttribute("radius");
            set => SetAttribute("radius", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Input != null)
            {
                write($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (Operator != null)
            {
                write($"{indent}{nameof(Operator)}: \"{Operator}\"");
            }
            if (Radius != null)
            {
                write($"{indent}{nameof(Radius)}: \"{Radius}\"");
            }
        }
    }
}
