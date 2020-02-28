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
            get => this.GetAttribute("viewBox");
            set => this.SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio", SvgNamespace)]
        public string? AspectRatio
        {
            get => this.GetAttribute("preserveAspectRatio");
            set => this.SetAttribute("preserveAspectRatio", value);
        }

        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y");
            set => this.SetAttribute("y", value);
        }

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => this.GetAttribute("width");
            set => this.SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => this.GetAttribute("height");
            set => this.SetAttribute("height", value);
        }

        [Attribute("patternUnits", SvgNamespace)]
        public string? PatternUnits
        {
            get => this.GetAttribute("patternUnits");
            set => this.SetAttribute("patternUnits", value);
        }

        [Attribute("patternContentUnits", SvgNamespace)]
        public string? PatternContentUnits
        {
            get => this.GetAttribute("patternContentUnits");
            set => this.SetAttribute("patternContentUnits", value);
        }

        [Attribute("patternTransform", SvgNamespace)]
        public string? PatternTransform
        {
            get => this.GetAttribute("patternTransform");
            set => this.SetAttribute("patternTransform", value);
        }

        [Attribute("href", XLinkNamespace)]
        public string? Href
        {
            get => this.GetAttribute("href");
            set => this.SetAttribute("href", value);
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
