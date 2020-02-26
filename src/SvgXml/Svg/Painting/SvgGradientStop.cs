using System;
using Xml;

namespace Svg
{
    [Element("stop")]
    public class SvgGradientStop : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("offset")]
        public string? Offset
        {
            get => GetAttribute("offset");
            set => SetAttribute("offset", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Offset != null)
            {
                Console.WriteLine($"{indent}{nameof(Offset)}: \"{Offset}\"");
            }
        }
    }
}
