using System;
using Xml;

namespace Svg
{
    [Element("pattern")]
    public class SvgPatternServer : SvgPaintServer,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("viewBox", SvgNamespace)]
        public string? ViewBox
        {
            get => GetAttribute("viewBox");
            set => SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio", SvgNamespace)]
        public string? AspectRatio
        {
            get => GetAttribute("preserveAspectRatio");
            set => SetAttribute("preserveAspectRatio", value);
        }

        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => GetAttribute("width");
            set => SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => GetAttribute("height");
            set => SetAttribute("height", value);
        }

        [Attribute("patternUnits", SvgNamespace)]
        public string? PatternUnits
        {
            get => GetAttribute("patternUnits");
            set => SetAttribute("patternUnits", value);
        }

        [Attribute("patternContentUnits", SvgNamespace)]
        public string? PatternContentUnits
        {
            get => GetAttribute("patternContentUnits");
            set => SetAttribute("patternContentUnits", value);
        }

        [Attribute("patternTransform", SvgNamespace)]
        public string? PatternTransform
        {
            get => GetAttribute("patternTransform");
            set => SetAttribute("patternTransform", value);
        }

        [Attribute("href", XLinkNamespace)]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (ViewBox != null)
            {
                write($"{indent}{nameof(ViewBox)}: \"{ViewBox}\"");
            }
            if (AspectRatio != null)
            {
                write($"{indent}{nameof(AspectRatio)}: \"{AspectRatio}\"");
            }
            if (X != null)
            {
                write($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                write($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Width != null)
            {
                write($"{indent}{nameof(Width)}: \"{Width}\"");
            }
            if (Height != null)
            {
                write($"{indent}{nameof(Height)}: \"{Height}\"");
            }
            if (PatternUnits != null)
            {
                write($"{indent}{nameof(PatternUnits)}: \"{PatternUnits}\"");
            }
            if (PatternContentUnits != null)
            {
                write($"{indent}{nameof(PatternContentUnits)}: \"{PatternContentUnits}\"");
            }
            if (PatternTransform != null)
            {
                write($"{indent}{nameof(PatternTransform)}: \"{PatternTransform}\"");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
