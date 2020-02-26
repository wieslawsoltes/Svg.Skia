using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("filter")]
    public class SvgFilter : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        [Attribute("x")]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y")]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("width")]
        public string? Width
        {
            get => GetAttribute("width");
            set => SetAttribute("width", value);
        }

        [Attribute("height")]
        public string? Height
        {
            get => GetAttribute("height");
            set => SetAttribute("height", value);
        }

        [Attribute("filterRes")]
        public string? FilterRes
        {
            get => GetAttribute("filterRes");
            set => SetAttribute("filterRes", value);
        }

        [Attribute("filterUnits")]
        public string? FilterUnits
        {
            get => GetAttribute("filterUnits");
            set => SetAttribute("filterUnits", value);
        }

        [Attribute("primitiveUnits")]
        public string? PrimitiveUnits
        {
            get => GetAttribute("primitiveUnits");
            set => SetAttribute("primitiveUnits", value);
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

            if (X != null)
            {
                Console.WriteLine($"{indent}{nameof(X)}='{X}'");
            }
            if (Y != null)
            {
                Console.WriteLine($"{indent}{nameof(Y)}='{Y}'");
            }
            if (Width != null)
            {
                Console.WriteLine($"{indent}{nameof(Width)}='{Width}'");
            }
            if (Height != null)
            {
                Console.WriteLine($"{indent}{nameof(Height)}='{Height}'");
            }
            if (FilterRes != null)
            {
                Console.WriteLine($"{indent}{nameof(FilterRes)}='{FilterRes}'");
            }
            if (FilterUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(FilterUnits)}='{FilterUnits}'");
            }
            if (PrimitiveUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(PrimitiveUnits)}='{PrimitiveUnits}'");
            }
            if (Href != null)
            {
                Console.WriteLine($"{indent}{nameof(Href)}='{Href}'");
            }
        }
    }
}
