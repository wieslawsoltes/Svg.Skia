using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feTurbulence")]
    public class SvgTurbulence : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("baseFrequency", SvgAttributes.SvgNamespace)]
        public string? BaseFrequency
        {
            get => GetAttribute("baseFrequency");
            set => SetAttribute("baseFrequency", value);
        }

        [Attribute("numOctaves", SvgAttributes.SvgNamespace)]
        public string? NumOctaves
        {
            get => GetAttribute("numOctaves");
            set => SetAttribute("numOctaves", value);
        }

        [Attribute("seed", SvgAttributes.SvgNamespace)]
        public string? Seed
        {
            get => GetAttribute("seed");
            set => SetAttribute("seed", value);
        }

        [Attribute("stitchTiles", SvgAttributes.SvgNamespace)]
        public string? StitchTiles
        {
            get => GetAttribute("stitchTiles");
            set => SetAttribute("stitchTiles", value);
        }

        [Attribute("type", SvgAttributes.SvgNamespace)]
        public string? Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (BaseFrequency != null)
            {
                write($"{indent}{nameof(BaseFrequency)}: \"{BaseFrequency}\"");
            }
            if (NumOctaves != null)
            {
                write($"{indent}{nameof(NumOctaves)}: \"{NumOctaves}\"");
            }
            if (Seed != null)
            {
                write($"{indent}{nameof(Seed)}: \"{Seed}\"");
            }
            if (StitchTiles != null)
            {
                write($"{indent}{nameof(StitchTiles)}: \"{StitchTiles}\"");
            }
            if (Type != null)
            {
                write($"{indent}{nameof(Type)}: \"{Type}\"");
            }
        }
    }
}
