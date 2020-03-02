using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feOffset")]
    public class SvgOffset : SvgFilterPrimitive,
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

        [Attribute("dx", SvgNamespace)]
        public string? Dx
        {
            get => this.GetAttribute("dx");
            set => this.SetAttribute("dx", value);
        }

        [Attribute("dy", SvgNamespace)]
        public string? Dy
        {
            get => this.GetAttribute("dy");
            set => this.SetAttribute("dy", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Input != null)
            {
                write($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (Dx != null)
            {
                write($"{indent}{nameof(Dx)}: \"{Dx}\"");
            }
            if (Dy != null)
            {
                write($"{indent}{nameof(Dy)}: \"{Dy}\"");
            }
        }
    }
}
