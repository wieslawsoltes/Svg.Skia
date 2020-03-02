using System;
using Xml;

namespace Svg
{
    [Element("style")]
    public class SvgStyle : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("type", SvgNamespace)]
        public string? Type
        {
            // TODO: https://www.w3.org/TR/SVG11/styling.html#StyleElement
            get => this.GetAttribute("type", false, null);
            set => this.SetAttribute("type", value);
        }

        [Attribute("media", SvgNamespace)]
        public string? Media
        {
            get => this.GetAttribute("media", false, "all");
            set => this.SetAttribute("media", value);
        }

        [Attribute("title", SvgNamespace)]
        public string? Title
        {
            get => this.GetAttribute("title", false, null);
            set => this.SetAttribute("title", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Type != null)
            {
                write($"{indent}{nameof(Type)}: \"{Type}\"");
            }
            if (Media != null)
            {
                write($"{indent}{nameof(Media)}: \"{Media}\"");
            }
            if (Title != null)
            {
                write($"{indent}{nameof(Title)}: \"{Title}\"");
            }
        }
    }
}
