using System;
using Xml;

namespace Svg
{
    [Element("color-profile")]
    public class SvgColorProfile : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("local", SvgNamespace)]
        public string? Local
        {
            get => this.GetAttribute("local");
            set => this.SetAttribute("local", value);
        }

        [Attribute("name", SvgNamespace)]
        public string? Name
        {
            get => this.GetAttribute("name");
            set => this.SetAttribute("name", value);
        }

        [Attribute("rendering-intent", SvgNamespace)]
        public string? RenderingIntent
        {
            get => this.GetAttribute("rendering-intent");
            set => this.SetAttribute("rendering-intent", value);
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
