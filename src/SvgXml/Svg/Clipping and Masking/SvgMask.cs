using System;
using Xml;

namespace Svg
{
    [Element("mask")]
    public class SvgMask : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
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

        [Attribute("maskUnits")]
        public string? MaskUnits
        {
            get => GetAttribute("maskUnits");
            set => SetAttribute("maskUnits", value);
        }

        [Attribute("maskContentUnits")]
        public string? MaskContentUnits
        {
            get => GetAttribute("maskContentUnits");
            set => SetAttribute("maskContentUnits", value);
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
            if (MaskUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(MaskUnits)}='{MaskUnits}'");
            }
            if (MaskContentUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(MaskContentUnits)}='{MaskContentUnits}'");
            }
        }
    }
}
