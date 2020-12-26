using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feTurbulence")]
    public class SvgTurbulence : SvgFilterPrimitive,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
        [Attribute("baseFrequency", SvgNamespace)]
        public string? BaseFrequency
        {
            get => this.GetAttribute("baseFrequency", false, "0");
            set => this.SetAttribute("baseFrequency", value);
        }

        [Attribute("numOctaves", SvgNamespace)]
        public string? NumOctaves
        {
            get => this.GetAttribute("numOctaves", false, "1");
            set => this.SetAttribute("numOctaves", value);
        }

        [Attribute("seed", SvgNamespace)]
        public string? Seed
        {
            get => this.GetAttribute("seed", false, "0");
            set => this.SetAttribute("seed", value);
        }

        [Attribute("stitchTiles", SvgNamespace)]
        public string? StitchTiles
        {
            get => this.GetAttribute("stitchTiles", false, "noStitch");
            set => this.SetAttribute("stitchTiles", value);
        }

        [Attribute("type", SvgNamespace)]
        public override string? Type
        {
            get => this.GetAttribute("type", false, "turbulence");
            set => this.SetAttribute("type", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "baseFrequency":
                    BaseFrequency = value;
                    break;
                case "numOctaves":
                    NumOctaves = value;
                    break;
                case "seed":
                    Seed = value;
                    break;
                case "stitchTiles":
                    StitchTiles = value;
                    break;
                case "type":
                    Type = value;
                    break;
            }
        }
    }
}
