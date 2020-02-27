using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feDistantLight")]
    public class SvgDistantLight : SvgElement
    {
        [Attribute("azimuth", SvgElement.SvgNamespace)]
        public string? Azimuth
        {
            get => GetAttribute("azimuth");
            set => SetAttribute("azimuth", value);
        }

        [Attribute("elevation", SvgElement.SvgNamespace)]
        public string? Elevation
        {
            get => GetAttribute("elevation");
            set => SetAttribute("elevation", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Azimuth != null)
            {
                write($"{indent}{nameof(Azimuth)}: \"{Azimuth}\"");
            }
            if (Elevation != null)
            {
                write($"{indent}{nameof(Elevation)}: \"{Elevation}\"");
            }
        }
    }
}
