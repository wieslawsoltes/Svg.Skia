using System;
using Xml;

namespace Svg
{
    [Element("radialGradient")]
    public class SvgRadialGradientServer : SvgGradientServer, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("cx")]
        public string? CenterX
        {
            get => GetAttribute("cx");
            set => SetAttribute("cx", value);
        }

        [Attribute("cy")]
        public string? CenterY
        {
            get => GetAttribute("cy");
            set => SetAttribute("cy", value);
        }

        [Attribute("r")]
        public string? Radius
        {
            get => GetAttribute("r");
            set => SetAttribute("r", value);
        }
        [Attribute("fx")]
        public string? FocalX
        {
            get => GetAttribute("fx");
            set => SetAttribute("fx", value);
        }

        [Attribute("fy")]
        public string? FocalY
        {
            get => GetAttribute("fy");
            set => SetAttribute("fy", value);
        }

        [Attribute("gradientUnits")]
        public string? GradientUnits
        {
            get => GetAttribute("gradientUnits");
            set => SetAttribute("gradientUnits", value);
        }

        [Attribute("gradientTransform")]
        public string? GradientTransform
        {
            get => GetAttribute("gradientTransform");
            set => SetAttribute("gradientTransform", value);
        }

        [Attribute("spreadMethod")]
        public string? SpreadMethod
        {
            get => GetAttribute("spreadMethod");
            set => SetAttribute("spreadMethod", value);
        }

        [Attribute("href")]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (CenterX != null)
            {
                Console.WriteLine($"{indent}{nameof(CenterX)}='{CenterX}'");
            }
            if (CenterY != null)
            {
                Console.WriteLine($"{indent}{nameof(CenterY)}='{CenterY}'");
            }
            if (Radius != null)
            {
                Console.WriteLine($"{indent}{nameof(Radius)}='{Radius}'");
            }
            if (FocalX != null)
            {
                Console.WriteLine($"{indent}{nameof(FocalX)}='{FocalX}'");
            }
            if (FocalY != null)
            {
                Console.WriteLine($"{indent}{nameof(FocalY)}='{FocalY}'");
            }
            if (GradientUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(GradientUnits)}='{GradientUnits}'");
            }
            if (GradientTransform != null)
            {
                Console.WriteLine($"{indent}{nameof(GradientTransform)}='{GradientTransform}'");
            }
            if (SpreadMethod != null)
            {
                Console.WriteLine($"{indent}{nameof(SpreadMethod)}='{SpreadMethod}'");
            }
            if (Href != null)
            {
                Console.WriteLine($"{indent}{nameof(Href)}='{Href}'");
            }
        }
    }
}
