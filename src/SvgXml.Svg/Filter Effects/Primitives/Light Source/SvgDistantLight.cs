using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feDistantLight")]
    public class SvgDistantLight : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("azimuth", SvgNamespace)]
        public string? Azimuth
        {
            get => this.GetAttribute("azimuth");
            set => this.SetAttribute("azimuth", value);
        }

        [Attribute("elevation", SvgNamespace)]
        public string? Elevation
        {
            get => this.GetAttribute("elevation");
            set => this.SetAttribute("elevation", value);
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
