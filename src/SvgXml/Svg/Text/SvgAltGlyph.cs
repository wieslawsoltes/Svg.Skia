using System;
using Xml;

namespace Svg
{
    [Element("altGlyph")]
    public class SvgAltGlyph : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        [Attribute("x", SvgElement.SvgNamespace)]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y", SvgElement.SvgNamespace)]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("dx", SvgElement.SvgNamespace)]
        public string? Dx
        {
            get => GetAttribute("dx");
            set => SetAttribute("dx", value);
        }

        [Attribute("dy", SvgElement.SvgNamespace)]
        public string? Dy
        {
            get => GetAttribute("dy");
            set => SetAttribute("dy", value);
        }

        [Attribute("glyphRef", SvgElement.SvgNamespace)]
        public string? GlyphRef
        {
            get => GetAttribute("glyphRef");
            set => SetAttribute("glyphRef", value);
        }

        [Attribute("format", SvgElement.SvgNamespace)]
        public string? Format
        {
            get => GetAttribute("format");
            set => SetAttribute("format", value);
        }

        [Attribute("rotate", SvgElement.SvgNamespace)]
        public string? Rotate
        {
            get => GetAttribute("rotate");
            set => SetAttribute("rotate", value);
        }

        [Attribute("href", SvgElement.XLinkNamespace)]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
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
