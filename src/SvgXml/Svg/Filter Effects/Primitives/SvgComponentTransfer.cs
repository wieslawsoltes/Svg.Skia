using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feComponentTransfer")]
    public class SvgComponentTransfer : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}: \"{Input}\"");
            }
        }
    }
}
