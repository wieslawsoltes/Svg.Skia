using System;
using Xml;

namespace Svg
{
    public interface ISvgStylableAttributes : IElement, ISvgAttributePrinter
    {
        [Attribute("class")]
        public string? Class
        {
            get => GetAttribute("class");
            set => SetAttribute("class", value);
        }

        [Attribute("style")]
        public string? Style
        {
            get => GetAttribute("style");
            set => SetAttribute("style", value);
        }

        public void PrintStylableAttributes(string indent)
        {
            if (Class != null)
            {
                Console.WriteLine($"{indent}{nameof(Class)}='{Class}'");
            }
            if (Style != null)
            {
                Console.WriteLine($"{indent}{nameof(Style)}='{Style}'");
            }
        }
    }
}
