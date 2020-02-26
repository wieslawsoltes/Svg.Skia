using System;
using Xml;


namespace Svg.FilterEffects
{
    [Element("feGaussianBlur")]
    public class SvgGaussianBlur : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("stdDeviation")]
        public string? StdDeviation
        {
            get => GetAttribute("stdDeviation");
            set => SetAttribute("stdDeviation", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}='{Input}'");
            }
            if (StdDeviation != null)
            {
                Console.WriteLine($"{indent}{nameof(StdDeviation)}='{StdDeviation}'");
            }
        }
    }
}
