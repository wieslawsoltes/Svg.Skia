using System;
using Xml;

namespace Svg
{
    public abstract class SvgKern : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("u1", SvgNamespace)]
        public string? Unicode1
        {
            get => this.GetAttribute("u1");
            set => this.SetAttribute("u1", value);
        }

        [Attribute("g1", SvgNamespace)]
        public string? Glyph1
        {
            get => this.GetAttribute("g1");
            set => this.SetAttribute("g1", value);
        }

        [Attribute("u2", SvgNamespace)]
        public string? Unicode2
        {
            get => this.GetAttribute("u2");
            set => this.SetAttribute("u2", value);
        }

        [Attribute("g2", SvgNamespace)]
        public string? Glyph2
        {
            get => this.GetAttribute("g2");
            set => this.SetAttribute("g2", value);
        }

        [Attribute("k", SvgNamespace)]
        public string? Kerning
        {
            get => this.GetAttribute("k");
            set => this.SetAttribute("k", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Unicode1 != null)
            {
                write($"{indent}{nameof(Unicode1)}: \"{Unicode1}\"");
            }
            if (Glyph1 != null)
            {
                write($"{indent}{nameof(Glyph1)}: \"{Glyph1}\"");
            }
            if (Unicode2 != null)
            {
                write($"{indent}{nameof(Unicode2)}: \"{Unicode2}\"");
            }
            if (Glyph2 != null)
            {
                write($"{indent}{nameof(Glyph2)}: \"{Glyph2}\"");
            }
            if (Kerning != null)
            {
                write($"{indent}{nameof(Kerning)}: \"{Kerning}\"");
            }
        }
    }
}
