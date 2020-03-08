using System;
using Xml;

namespace Svg
{
    [Element("altGlyph")]
    public class SvgAltGlyph : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x", false, null);
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y", false, null);
            set => this.SetAttribute("y", value);
        }

        [Attribute("dx", SvgNamespace)]
        public string? Dx
        {
            get => this.GetAttribute("dx", false, null);
            set => this.SetAttribute("dx", value);
        }

        [Attribute("dy", SvgNamespace)]
        public string? Dy
        {
            get => this.GetAttribute("dy", false, null);
            set => this.SetAttribute("dy", value);
        }

        [Attribute("glyphRef", SvgNamespace)]
        public string? GlyphRef
        {
            get => this.GetAttribute("glyphRef", false, null);
            set => this.SetAttribute("glyphRef", value);
        }

        [Attribute("format", SvgNamespace)]
        public string? Format
        {
            get => this.GetAttribute("format", false, null);
            set => this.SetAttribute("format", value);
        }

        [Attribute("rotate", SvgNamespace)]
        public string? Rotate
        {
            get => this.GetAttribute("rotate", false, null);
            set => this.SetAttribute("rotate", value);
        }

        [Attribute("href", XLinkNamespace)]
        public override string? Href
        {
            get => this.GetAttribute("href", false, null);
            set => this.SetAttribute("href", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "x":
                    X = value;
                    break;
                case "y":
                    Y = value;
                    break;
                case "dx":
                    Dx = value;
                    break;
                case "dy":
                    Dy = value;
                    break;
                case "glyphRef":
                    GlyphRef = value;
                    break;
                case "format":
                    Format = value;
                    break;
                case "rotate":
                    Rotate = value;
                    break;
                case "href":
                    Href = value;
                    break;
            }
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (X != null)
            {
                write($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                write($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Dx != null)
            {
                write($"{indent}{nameof(Dx)}: \"{Dx}\"");
            }
            if (Dy != null)
            {
                write($"{indent}{nameof(Dy)}: \"{Dy}\"");
            }
            if (GlyphRef != null)
            {
                write($"{indent}{nameof(GlyphRef)}: \"{GlyphRef}\"");
            }
            if (Format != null)
            {
                write($"{indent}{nameof(Format)}: \"{Format}\"");
            }
            if (Rotate != null)
            {
                write($"{indent}{nameof(Rotate)}: \"{Rotate}\"");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
