using System;
using Xml;

namespace Svg.FilterEffects
{
    public abstract class SvgComponentTransferFunction : SvgElement
    {
        [Attribute("type", SvgElement.SvgNamespace)]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        [Attribute("tableValues", SvgElement.SvgNamespace)]
        public string? TableValues
        {
            get => GetAttribute("tableValues");
            set => SetAttribute("tableValues", value);
        }

        [Attribute("slope", SvgElement.SvgNamespace)]
        public string? Slope
        {
            get => GetAttribute("slope");
            set => SetAttribute("slope", value);
        }

        [Attribute("intercept", SvgElement.SvgNamespace)]
        public string? Intercept
        {
            get => GetAttribute("intercept");
            set => SetAttribute("intercept", value);
        }

        [Attribute("amplitude", SvgElement.SvgNamespace)]
        public string? Amplitude
        {
            get => GetAttribute("amplitude");
            set => SetAttribute("amplitude", value);
        }

        [Attribute("exponent", SvgElement.SvgNamespace)]
        public string? Exponent
        {
            get => GetAttribute("exponent");
            set => SetAttribute("exponent", value);
        }

        [Attribute("offset", SvgElement.SvgNamespace)]
        public string? Offset
        {
            get => GetAttribute("offset");
            set => SetAttribute("offset", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Type != null)
            {
                write($"{indent}{nameof(Type)}: \"{Type}\"");
            }
            if (TableValues != null)
            {
                write($"{indent}{nameof(TableValues)}: \"{TableValues}\"");
            }
            if (Slope != null)
            {
                write($"{indent}{nameof(Slope)}: \"{Slope}\"");
            }
            if (Intercept != null)
            {
                write($"{indent}{nameof(Intercept)}: \"{Intercept}\"");
            }
            if (Amplitude != null)
            {
                write($"{indent}{nameof(Amplitude)}: \"{Amplitude}\"");
            }
            if (Exponent != null)
            {
                write($"{indent}{nameof(Exponent)}: \"{Exponent}\"");
            }
            if (Offset != null)
            {
                write($"{indent}{nameof(Offset)}: \"{Offset}\"");
            }
        }
    }
}
