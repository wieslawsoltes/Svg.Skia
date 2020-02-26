using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feComposite")]
    public class SvgComposite : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("in2")]
        public string? Input2
        {
            get => GetAttribute("in2");
            set => SetAttribute("in2", value);
        }

        [Attribute("operator")]
        public string? Operator
        {
            get => GetAttribute("operator");
            set => SetAttribute("operator", value);
        }

        [Attribute("k1")]
        public string? K1
        {
            get => GetAttribute("k1");
            set => SetAttribute("k1", value);
        }

        [Attribute("k2")]
        public string? K2
        {
            get => GetAttribute("k2");
            set => SetAttribute("k2", value);
        }

        [Attribute("k3")]
        public string? K3
        {
            get => GetAttribute("k3");
            set => SetAttribute("k3", value);
        }

        [Attribute("k4")]
        public string? K4
        {
            get => GetAttribute("k4");
            set => SetAttribute("k4", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}='{Input}'");
            }
            if (Input2 != null)
            {
                Console.WriteLine($"{indent}{nameof(Input2)}='{Input2}'");
            }
            if (Operator != null)
            {
                Console.WriteLine($"{indent}{nameof(Operator)}='{Operator}'");
            }
            if (K1 != null)
            {
                Console.WriteLine($"{indent}{nameof(K1)}='{K1}'");
            }
            if (K2 != null)
            {
                Console.WriteLine($"{indent}{nameof(K2)}='{K2}'");
            }
            if (K3 != null)
            {
                Console.WriteLine($"{indent}{nameof(K3)}='{K3}'");
            }
            if (K4 != null)
            {
                Console.WriteLine($"{indent}{nameof(K4)}='{K4}'");
            }
        }
    }
}
