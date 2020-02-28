using System;
using Xml;

namespace Svg.FilterEffects
{
    public abstract class SvgComponentTransferFunction : SvgElement
    {
        [Attribute("type", SvgNamespace)]
        public string? Type
        {
            get => this.GetAttribute("type");
            set => this.SetAttribute("type", value);
        }

        [Attribute("tableValues", SvgNamespace)]
        public string? TableValues
        {
            get => this.GetAttribute("tableValues");
            set => this.SetAttribute("tableValues", value);
        }

        [Attribute("slope", SvgNamespace)]
        public string? Slope
        {
            get => this.GetAttribute("slope");
            set => this.SetAttribute("slope", value);
        }

        [Attribute("intercept", SvgNamespace)]
        public string? Intercept
        {
            get => this.GetAttribute("intercept");
            set => this.SetAttribute("intercept", value);
        }

        [Attribute("amplitude", SvgNamespace)]
        public string? Amplitude
        {
            get => this.GetAttribute("amplitude");
            set => this.SetAttribute("amplitude", value);
        }

        [Attribute("exponent", SvgNamespace)]
        public string? Exponent
        {
            get => this.GetAttribute("exponent");
            set => this.SetAttribute("exponent", value);
        }

        [Attribute("offset", SvgNamespace)]
        public string? Offset
        {
            get => this.GetAttribute("offset");
            set => this.SetAttribute("offset", value);
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
