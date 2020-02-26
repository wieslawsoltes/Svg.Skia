using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feDistantLight")]
    public class SvgDistantLight : SvgElement
    {
        [Attribute("azimuth")]
        public string? Azimuth
        {
            get => GetAttribute("azimuth");
            set => SetAttribute("azimuth", value);
        }

        [Attribute("elevation")]
        public string? Elevation
        {
            get => GetAttribute("elevation");
            set => SetAttribute("elevation", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Azimuth != null)
            {
                Console.WriteLine($"{indent}{nameof(Azimuth)}='{Azimuth}'");
            }
            if (Elevation != null)
            {
                Console.WriteLine($"{indent}{nameof(Elevation)}='{Elevation}'");
            }
        }
    }
}
