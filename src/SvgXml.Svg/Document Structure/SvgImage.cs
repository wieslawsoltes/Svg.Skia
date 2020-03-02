using System;
using Xml;

namespace Svg
{
    [Element("image")]
    public class SvgImage : SvgVisualElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
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

        [Attribute("href", XLinkNamespace)]
        public string? Href
        {
            get => this.GetAttribute("href");
            set => this.SetAttribute("href", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

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
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
