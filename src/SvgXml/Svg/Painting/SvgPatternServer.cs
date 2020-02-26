using System;
using Xml;

namespace Svg
{
    [Element("pattern")]
    public class SvgPatternServer : SvgPaintServer, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
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

        [Attribute("patternUnits")]
        public string? PatternUnits
        {
            get => GetAttribute("patternUnits");
            set => SetAttribute("patternUnits", value);
        }

        [Attribute("patternContentUnits")]
        public string? PatternContentUnits
        {
            get => GetAttribute("patternContentUnits");
            set => SetAttribute("patternContentUnits", value);
        }

        [Attribute("patternTransform")]
        public string? PatternTransform
        {
            get => GetAttribute("patternTransform");
            set => SetAttribute("patternTransform", value);
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

            if (ViewBox != null)
            {
                Console.WriteLine($"{indent}{nameof(ViewBox)}: \"{ViewBox}\"");
            }
            if (AspectRatio != null)
            {
                Console.WriteLine($"{indent}{nameof(AspectRatio)}: \"{AspectRatio}\"");
            }
            if (X != null)
            {
                Console.WriteLine($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                Console.WriteLine($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Width != null)
            {
                Console.WriteLine($"{indent}{nameof(Width)}: \"{Width}\"");
            }
            if (Height != null)
            {
                Console.WriteLine($"{indent}{nameof(Height)}: \"{Height}\"");
            }
            if (PatternUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(PatternUnits)}: \"{PatternUnits}\"");
            }
            if (PatternContentUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(PatternContentUnits)}: \"{PatternContentUnits}\"");
            }
            if (PatternTransform != null)
            {
                Console.WriteLine($"{indent}{nameof(PatternTransform)}: \"{PatternTransform}\"");
            }
            if (Href != null)
            {
                Console.WriteLine($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
