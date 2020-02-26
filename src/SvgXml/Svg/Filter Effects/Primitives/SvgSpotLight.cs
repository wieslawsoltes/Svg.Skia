using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feSpotLight")]
    public class SvgSpotLight : SvgElement
    {
        [Attribute("x")]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y")]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("z")]
        public string? Z
        {
            get => GetAttribute("z");
            set => SetAttribute("z", value);
        }

        [Attribute("pointsAtX")]
        public string? PointsAtX
        {
            get => GetAttribute("pointsAtX");
            set => SetAttribute("pointsAtX", value);
        }

        [Attribute("pointsAtY")]
        public string? PointsAtY
        {
            get => GetAttribute("pointsAtY");
            set => SetAttribute("pointsAtY", value);
        }

        [Attribute("pointsAtZ")]
        public string? PointsAtZ
        {
            get => GetAttribute("pointsAtZ");
            set => SetAttribute("pointsAtZ", value);
        }

        [Attribute("specularExponent")]
        public string? SpecularExponent
        {
            get => GetAttribute("specularExponent");
            set => SetAttribute("specularExponent", value);
        }

        [Attribute("limitingConeAngle")]
        public string? LlimitingConeAngle
        {
            get => GetAttribute("limitingConeAngle");
            set => SetAttribute("limitingConeAngle", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (X != null)
            {
                Console.WriteLine($"{indent}{nameof(X)}='{X}'");
            }
            if (Y != null)
            {
                Console.WriteLine($"{indent}{nameof(Y)}='{Y}'");
            }
            if (Z != null)
            {
                Console.WriteLine($"{indent}{nameof(Z)}='{Z}'");
            }
            if (PointsAtX != null)
            {
                Console.WriteLine($"{indent}{nameof(PointsAtX)}='{PointsAtX}'");
            }
            if (PointsAtY != null)
            {
                Console.WriteLine($"{indent}{nameof(PointsAtY)}='{PointsAtY}'");
            }
            if (PointsAtZ != null)
            {
                Console.WriteLine($"{indent}{nameof(PointsAtZ)}='{PointsAtZ}'");
            }
            if (SpecularExponent != null)
            {
                Console.WriteLine($"{indent}{nameof(SpecularExponent)}='{SpecularExponent}'");
            }
            if (LlimitingConeAngle != null)
            {
                Console.WriteLine($"{indent}{nameof(LlimitingConeAngle)}='{LlimitingConeAngle}'");
            }
        }
    }
}
