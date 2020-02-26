using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("fePointLight")]
    public class SvgPointLight : SvgElement
    {
        [Attribute("x", SvgAttributes.SvgNamespace)]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y", SvgAttributes.SvgNamespace)]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("z", SvgAttributes.SvgNamespace)]
        public string? Z
        {
            get => GetAttribute("z");
            set => SetAttribute("z", value);
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
