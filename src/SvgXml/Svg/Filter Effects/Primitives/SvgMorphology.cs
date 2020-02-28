using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feMorphology")]
    public class SvgMorphology : SvgFilterPrimitive,
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

        [Attribute("operator", SvgNamespace)]
        public string? Operator
        {
            get => this.GetAttribute("operator");
            set => this.SetAttribute("operator", value);
        }

        [Attribute("radius", SvgNamespace)]
        public string? Radius
        {
            get => this.GetAttribute("radius");
            set => this.SetAttribute("radius", value);
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
