using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("fePointLight")]
    public class SvgPointLight : SvgElement
    {
        [Attribute("x")]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y")]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("z")]
        public string? Z
        {
            get => GetAttribute("z");
            set => SetAttribute("z", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (X != null)
            {
                Console.WriteLine($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                Console.WriteLine($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Z != null)
            {
                Console.WriteLine($"{indent}{nameof(Z)}: \"{Z}\"");
            }
        }
    }
}
