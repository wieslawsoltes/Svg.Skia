using System;
using Xml;

namespace Svg
{
    [Element("symbol")]
    public class SvgSymbol : SvgVisualElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("viewBox")]
        public string? ViewBox
        {
            get => GetAttribute("viewBox");
            set => SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio")]
        public string? AspectRatio
        {
            get => GetAttribute("preserveAspectRatio");
            set => SetAttribute("preserveAspectRatio", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (ViewBox != null)
            {
                Console.WriteLine($"{indent}{nameof(ViewBox)}='{ViewBox}'");
            }
            if (AspectRatio != null)
            {
                Console.WriteLine($"{indent}{nameof(AspectRatio)}='{AspectRatio}'");
            }
        }
    }
}
