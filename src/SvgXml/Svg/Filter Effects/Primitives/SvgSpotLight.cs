using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feSpotLight")]
    public class SvgSpotLight : SvgElement
    {
        [Attribute("x", SvgAttributes.SvgNamespace)]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y", SvgAttributes.SvgNamespace)]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("z", SvgAttributes.SvgNamespace)]
        public string? Z
        {
            get => GetAttribute("z");
            set => SetAttribute("z", value);
        }

        [Attribute("pointsAtX", SvgAttributes.SvgNamespace)]
        public string? PointsAtX
        {
            get => GetAttribute("pointsAtX");
            set => SetAttribute("pointsAtX", value);
        }

        [Attribute("pointsAtY", SvgAttributes.SvgNamespace)]
        public string? PointsAtY
        {
            get => GetAttribute("pointsAtY");
            set => SetAttribute("pointsAtY", value);
        }

        [Attribute("pointsAtZ", SvgAttributes.SvgNamespace)]
        public string? PointsAtZ
        {
            get => GetAttribute("pointsAtZ");
            set => SetAttribute("pointsAtZ", value);
        }

        [Attribute("specularExponent", SvgAttributes.SvgNamespace)]
        public string? SpecularExponent
        {
            get => GetAttribute("specularExponent");
            set => SetAttribute("specularExponent", value);
        }

        [Attribute("limitingConeAngle", SvgAttributes.SvgNamespace)]
        public string? LlimitingConeAngle
        {
            get => GetAttribute("limitingConeAngle");
            set => SetAttribute("limitingConeAngle", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (X != null)
            {
                write($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                write($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Z != null)
            {
                write($"{indent}{nameof(Z)}: \"{Z}\"");
            }
            if (PointsAtX != null)
            {
                write($"{indent}{nameof(PointsAtX)}: \"{PointsAtX}\"");
            }
            if (PointsAtY != null)
            {
                write($"{indent}{nameof(PointsAtY)}: \"{PointsAtY}\"");
            }
            if (PointsAtZ != null)
            {
                write($"{indent}{nameof(PointsAtZ)}: \"{PointsAtZ}\"");
            }
            if (SpecularExponent != null)
            {
                write($"{indent}{nameof(SpecularExponent)}: \"{SpecularExponent}\"");
            }
            if (LlimitingConeAngle != null)
            {
                write($"{indent}{nameof(LlimitingConeAngle)}: \"{LlimitingConeAngle}\"");
            }
        }
    }
}
