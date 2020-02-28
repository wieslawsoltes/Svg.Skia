using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feSpotLight")]
    public class SvgSpotLight : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y");
            set => this.SetAttribute("y", value);
        }

        [Attribute("z", SvgNamespace)]
        public string? Z
        {
            get => this.GetAttribute("z");
            set => this.SetAttribute("z", value);
        }

        [Attribute("pointsAtX", SvgNamespace)]
        public string? PointsAtX
        {
            get => this.GetAttribute("pointsAtX");
            set => this.SetAttribute("pointsAtX", value);
        }

        [Attribute("pointsAtY", SvgNamespace)]
        public string? PointsAtY
        {
            get => this.GetAttribute("pointsAtY");
            set => this.SetAttribute("pointsAtY", value);
        }

        [Attribute("pointsAtZ", SvgNamespace)]
        public string? PointsAtZ
        {
            get => this.GetAttribute("pointsAtZ");
            set => this.SetAttribute("pointsAtZ", value);
        }

        [Attribute("specularExponent", SvgNamespace)]
        public string? SpecularExponent
        {
            get => this.GetAttribute("specularExponent");
            set => this.SetAttribute("specularExponent", value);
        }

        [Attribute("limitingConeAngle", SvgNamespace)]
        public string? LimitingConeAngle
        {
            get => this.GetAttribute("limitingConeAngle");
            set => this.SetAttribute("limitingConeAngle", value);
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
            if (LimitingConeAngle != null)
            {
                write($"{indent}{nameof(LimitingConeAngle)}: \"{LimitingConeAngle}\"");
            }
        }
    }
}
