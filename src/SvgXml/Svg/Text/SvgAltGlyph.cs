using System;
using Xml;

namespace Svg
{
    [Element("altGlyph")]
    public class SvgAltGlyph : SvgElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
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

        [Attribute("dx", SvgNamespace)]
        public string? Dx
        {
            get => this.GetAttribute("dx");
            set => this.SetAttribute("dx", value);
        }

        [Attribute("dy", SvgNamespace)]
        public string? Dy
        {
            get => this.GetAttribute("dy");
            set => this.SetAttribute("dy", value);
        }

        [Attribute("glyphRef", SvgNamespace)]
        public string? GlyphRef
        {
            get => this.GetAttribute("glyphRef");
            set => this.SetAttribute("glyphRef", value);
        }

        [Attribute("format", SvgNamespace)]
        public string? Format
        {
            get => this.GetAttribute("format");
            set => this.SetAttribute("format", value);
        }

        [Attribute("rotate", SvgNamespace)]
        public string? Rotate
        {
            get => this.GetAttribute("rotate");
            set => this.SetAttribute("rotate", value);
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
