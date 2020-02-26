using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feImage")]
    public class SvgImage : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        [Attribute("preserveAspectRatio")]
        public string? AspectRatio
        {
            get => GetAttribute("preserveAspectRatio");
            set => SetAttribute("preserveAspectRatio", value);
        }

        [Attribute("href")]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (AspectRatio != null)
            {
                Console.WriteLine($"{indent}{nameof(AspectRatio)}: \"{AspectRatio}\"");
            }
            if (Href != null)
            {
                Console.WriteLine($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
