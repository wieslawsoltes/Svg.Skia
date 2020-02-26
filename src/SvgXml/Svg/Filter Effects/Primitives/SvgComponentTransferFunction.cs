using System;
using Xml;

namespace Svg.FilterEffects
{
    public abstract class SvgComponentTransferFunction : SvgElement
    {
        [Attribute("type")]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        [Attribute("tableValues")]
        public string? TableValues
        {
            get => GetAttribute("tableValues");
            set => SetAttribute("tableValues", value);
        }

        [Attribute("slope")]
        public string? Slope
        {
            get => GetAttribute("slope");
            set => SetAttribute("slope", value);
        }

        [Attribute("intercept")]
        public string? Intercept
        {
            get => GetAttribute("intercept");
            set => SetAttribute("intercept", value);
        }

        [Attribute("amplitude")]
        public string? Amplitude
        {
            get => GetAttribute("amplitude");
            set => SetAttribute("amplitude", value);
        }

        [Attribute("exponent")]
        public string? Exponent
        {
            get => GetAttribute("exponent");
            set => SetAttribute("exponent", value);
        }

        [Attribute("offset")]
        public string? Offset
        {
            get => GetAttribute("offset");
            set => SetAttribute("offset", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Type != null)
            {
                Console.WriteLine($"{indent}{nameof(Type)}: \"{Type}\"");
            }
            if (TableValues != null)
            {
                Console.WriteLine($"{indent}{nameof(TableValues)}: \"{TableValues}\"");
            }
            if (Slope != null)
            {
                Console.WriteLine($"{indent}{nameof(Slope)}: \"{Slope}\"");
            }
            if (Intercept != null)
            {
                Console.WriteLine($"{indent}{nameof(Intercept)}: \"{Intercept}\"");
            }
            if (Amplitude != null)
            {
                Console.WriteLine($"{indent}{nameof(Amplitude)}: \"{Amplitude}\"");
            }
            if (Exponent != null)
            {
                Console.WriteLine($"{indent}{nameof(Exponent)}: \"{Exponent}\"");
            }
            if (Offset != null)
            {
                Console.WriteLine($"{indent}{nameof(Offset)}: \"{Offset}\"");
            }
        }
    }
}
