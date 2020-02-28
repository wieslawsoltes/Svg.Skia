using System;
using Xml;

namespace Svg
{
    [Element("glyph")]
    public class SvgGlyph : SvgPathBasedElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
        [Attribute("d", SvgNamespace)]
        public string? PathData
        {
            get => GetAttribute("d");
            set => SetAttribute("d", value);
        }

        [Attribute("horiz-adv-x", SvgNamespace)]
        public string? HorizAdvX
        {
            get => GetAttribute("horiz-adv-x");
            set => SetAttribute("horiz-adv-x", value);
        }

        [Attribute("vert-origin-x", SvgNamespace)]
        public string? VertOriginX
        {
            get => GetAttribute("vert-origin-x");
            set => SetAttribute("vert-origin-x", value);
        }

        [Attribute("vert-origin-y", SvgNamespace)]
        public string? VertOriginY
        {
            get => GetAttribute("vert-origin-y");
            set => SetAttribute("vert-origin-y", value);
        }

        [Attribute("vert-adv-y", SvgNamespace)]
        public string? VertAdvY
        {
            get => GetAttribute("vert-adv-y");
            set => SetAttribute("vert-adv-y", value);
        }

        [Attribute("unicode", SvgNamespace)]
        public string? Unicode
        {
            get => GetAttribute("unicode");
            set => SetAttribute("unicode", value);
        }

        [Attribute("glyph-name", SvgNamespace)]
        public string? GlyphName
        {
            get => GetAttribute("glyph-name");
            set => SetAttribute("glyph-name", value);
        }

        [Attribute("orientation", SvgNamespace)]
        public string? Orientation
        {
            get => GetAttribute("orientation");
            set => SetAttribute("orientation", value);
        }

        [Attribute("arabic-form", SvgNamespace)]
        public string? ArabicForm
        {
            get => GetAttribute("arabic-form");
            set => SetAttribute("arabic-form", value);
        }

        [Attribute("lang", SvgNamespace)]
        public string? Lang
        {
            get => GetAttribute("lang");
            set => SetAttribute("lang", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (PathData != null)
            {
                write($"{indent}{nameof(PathData)}: \"{PathData}\"");
            }
            if (HorizAdvX != null)
            {
                write($"{indent}{nameof(HorizAdvX)}: \"{HorizAdvX}\"");
            }
            if (VertOriginX != null)
            {
                write($"{indent}{nameof(VertOriginX)}: \"{VertOriginX}\"");
            }
            if (VertOriginY != null)
            {
                write($"{indent}{nameof(VertOriginY)}: \"{VertOriginY}\"");
            }
            if (VertAdvY != null)
            {
                write($"{indent}{nameof(VertAdvY)}: \"{VertAdvY}\"");
            }
            if (Unicode != null)
            {
                write($"{indent}{nameof(Unicode)}: \"{Unicode}\"");
            }
            if (GlyphName != null)
            {
                write($"{indent}{nameof(GlyphName)}: \"{GlyphName}\"");
            }
            if (Orientation != null)
            {
                write($"{indent}{nameof(Orientation)}: \"{Orientation}\"");
            }
            if (ArabicForm != null)
            {
                write($"{indent}{nameof(ArabicForm)}: \"{ArabicForm}\"");
            }
            if (Lang != null)
            {
                write($"{indent}{nameof(Lang)}: \"{Lang}\"");
            }
        }
    }
}
