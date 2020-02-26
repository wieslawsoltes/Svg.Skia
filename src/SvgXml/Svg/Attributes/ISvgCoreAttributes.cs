using System;
using Xml;

namespace Svg
{
    public interface ISvgCoreAttributes : IElement, ISvgAttributePrinter
    {
        [Attribute("id")]
        public string? Id
        {
            get => GetAttribute("id");
            set => SetAttribute("id", value);
        }

        [Attribute("base")]
        public string? Base
        {
            get => GetAttribute("base");
            set => SetAttribute("base", value);
        }

        [Attribute("lang")]
        public string? Lang
        {
            get => GetAttribute("lang");
            set => SetAttribute("lang", value);
        }

        [Attribute("space")]
        public string? Space
        {
            get => GetAttribute("space");
            set => SetAttribute("space", value);
        }

        public void PrintCoreAttributes(string indent)
        {
            if (Id != null)
            {
                Console.WriteLine($"{indent}{nameof(Id)}: \"{Id}\"");
            }
            if (Base != null)
            {
                Console.WriteLine($"{indent}{nameof(Base)}: \"{Base}\"");
            }
            if (Lang != null)
            {
                Console.WriteLine($"{indent}{nameof(Lang)}: \"{Lang}\"");
            }
            if (Space != null)
            {
                Console.WriteLine($"{indent}{nameof(Space)}: \"{Space}\"");
            }
        }
    }
}
