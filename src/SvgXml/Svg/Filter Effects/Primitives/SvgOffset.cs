using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feOffset")]
    public class SvgOffset : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("dx")]
        public string? Dx
        {
            get => GetAttribute("dx");
            set => SetAttribute("dx", value);
        }

        [Attribute("dy")]
        public string? Dy
        {
            get => GetAttribute("dy");
            set => SetAttribute("dy", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}='{Input}'");
            }
            if (Dx != null)
            {
                Console.WriteLine($"{indent}{nameof(Dx)}='{Dx}'");
            }
            if (Dy != null)
            {
                Console.WriteLine($"{indent}{nameof(Dy)}='{Dy}'");
            }
        }
    }
}
