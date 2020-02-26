using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feMorphology")]
    public class SvgMorphology : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("operator")]
        public string? Operator
        {
            get => GetAttribute("operator");
            set => SetAttribute("operator", value);
        }

        [Attribute("radius")]
        public string? Radius
        {
            get => GetAttribute("radius");
            set => SetAttribute("radius", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}='{Input}'");
            }
            if (Operator != null)
            {
                Console.WriteLine($"{indent}{nameof(Operator)}='{Operator}'");
            }
            if (Radius != null)
            {
                Console.WriteLine($"{indent}{nameof(Radius)}='{Radius}'");
            }
        }
    }
}
