using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("fePointLight")]
    public class SvgPointLight : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y");
            set => this.SetAttribute("y", value);
        }

        [Attribute("z", SvgNamespace)]
        public string? Z
        {
            get => this.GetAttribute("z");
            set => this.SetAttribute("z", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (X != null)
            {
                write($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                write($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Z != null)
            {
                write($"{indent}{nameof(Z)}: \"{Z}\"");
            }
        }
    }
}
