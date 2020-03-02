using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feComposite")]
    public class SvgComposite : SvgFilterPrimitive,
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

        [Attribute("in2", SvgNamespace)]
        public string? Input2
        {
            get => this.GetAttribute("in2");
            set => this.SetAttribute("in2", value);
        }

        [Attribute("operator", SvgNamespace)]
        public string? Operator
        {
            get => this.GetAttribute("operator");
            set => this.SetAttribute("operator", value);
        }

        [Attribute("k1", SvgNamespace)]
        public string? K1
        {
            get => this.GetAttribute("k1");
            set => this.SetAttribute("k1", value);
        }

        [Attribute("k2", SvgNamespace)]
        public string? K2
        {
            get => this.GetAttribute("k2");
            set => this.SetAttribute("k2", value);
        }

        [Attribute("k3", SvgNamespace)]
        public string? K3
        {
            get => this.GetAttribute("k3");
            set => this.SetAttribute("k3", value);
        }

        [Attribute("k4", SvgNamespace)]
        public string? K4
        {
            get => this.GetAttribute("k4");
            set => this.SetAttribute("k4", value);
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
            if (Operator != null)
            {
                write($"{indent}{nameof(Operator)}: \"{Operator}\"");
            }
            if (K1 != null)
            {
                write($"{indent}{nameof(K1)}: \"{K1}\"");
            }
            if (K2 != null)
            {
                write($"{indent}{nameof(K2)}: \"{K2}\"");
            }
            if (K3 != null)
            {
                write($"{indent}{nameof(K3)}: \"{K3}\"");
            }
            if (K4 != null)
            {
                write($"{indent}{nameof(K4)}: \"{K4}\"");
            }
        }
    }
}
