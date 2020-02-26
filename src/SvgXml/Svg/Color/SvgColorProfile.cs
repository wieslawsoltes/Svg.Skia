using System;
using Xml;

namespace Svg
{
    [Element("color-profile")]
    public class SvgColorProfile : SvgElement
    {
        [Attribute("local")]
        public string? Local
        {
            get => GetAttribute("local");
            set => SetAttribute("local", value);
        }

        [Attribute("name")]
        public string? Name
        {
            get => GetAttribute("name");
            set => SetAttribute("name", value);
        }

        [Attribute("rendering-intent")]
        public string? RenderingIntent
        {
            get => GetAttribute("rendering-intent");
            set => SetAttribute("rendering-intent", value);
        }

        [Attribute("href")]
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
                Console.WriteLine($"{indent}{nameof(Local)}='{Local}'");
            }
            if (Name != null)
            {
                Console.WriteLine($"{indent}{nameof(Name)}='{Name}'");
            }
            if (RenderingIntent != null)
            {
                Console.WriteLine($"{indent}{nameof(RenderingIntent)}='{RenderingIntent}'");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
