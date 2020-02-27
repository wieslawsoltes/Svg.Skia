using System;
using Xml;

namespace Svg
{
    [Element("color-profile")]
    public class SvgColorProfile : SvgElement
    {
        [Attribute("local", SvgElement.SvgNamespace)]
        public string? Local
        {
            get => GetAttribute("local");
            set => SetAttribute("local", value);
        }

        [Attribute("name", SvgElement.SvgNamespace)]
        public string? Name
        {
            get => GetAttribute("name");
            set => SetAttribute("name", value);
        }

        [Attribute("rendering-intent", SvgElement.SvgNamespace)]
        public string? RenderingIntent
        {
            get => GetAttribute("rendering-intent");
            set => SetAttribute("rendering-intent", value);
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

            if (Local != null)
            {
                write($"{indent}{nameof(Local)}: \"{Local}\"");
            }
            if (Name != null)
            {
                write($"{indent}{nameof(Name)}: \"{Name}\"");
            }
            if (RenderingIntent != null)
            {
                write($"{indent}{nameof(RenderingIntent)}: \"{RenderingIntent}\"");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
