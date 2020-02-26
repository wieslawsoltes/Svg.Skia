using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feTurbulence")]
    public class SvgTurbulence : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("baseFrequency")]
        public string? BaseFrequency
        {
            get => GetAttribute("baseFrequency");
            set => SetAttribute("baseFrequency", value);
        }

        [Attribute("numOctaves")]
        public string? NumOctaves
        {
            get => GetAttribute("numOctaves");
            set => SetAttribute("numOctaves", value);
        }

        [Attribute("seed")]
        public string? Seed
        {
            get => GetAttribute("seed");
            set => SetAttribute("seed", value);
        }

        [Attribute("stitchTiles")]
        public string? StitchTiles
        {
            get => GetAttribute("stitchTiles");
            set => SetAttribute("stitchTiles", value);
        }

        [Attribute("type")]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (BaseFrequency != null)
            {
                Console.WriteLine($"{indent}{nameof(BaseFrequency)}: \"{BaseFrequency}\"");
            }
            if (NumOctaves != null)
            {
                Console.WriteLine($"{indent}{nameof(NumOctaves)}: \"{NumOctaves}\"");
            }
            if (Seed != null)
            {
                Console.WriteLine($"{indent}{nameof(Seed)}: \"{Seed}\"");
            }
            if (StitchTiles != null)
            {
                Console.WriteLine($"{indent}{nameof(StitchTiles)}: \"{StitchTiles}\"");
            }
            if (Type != null)
            {
                Console.WriteLine($"{indent}{nameof(Type)}: \"{Type}\"");
            }
        }
    }
}
