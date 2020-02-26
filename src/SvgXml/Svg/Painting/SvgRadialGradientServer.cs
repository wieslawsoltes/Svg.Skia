using System;
using Xml;

namespace Svg
{
    [Element("radialGradient")]
    public class SvgRadialGradientServer : SvgGradientServer, ISvgPresentationAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        [Attribute("cx", SvgAttributes.SvgNamespace)]
        public string? CenterX
        {
            get => GetAttribute("cx");
            set => SetAttribute("cx", value);
        }

        [Attribute("cy", SvgAttributes.SvgNamespace)]
        public string? CenterY
        {
            get => GetAttribute("cy");
            set => SetAttribute("cy", value);
        }

        [Attribute("r", SvgAttributes.SvgNamespace)]
        public string? Radius
        {
            get => GetAttribute("r");
            set => SetAttribute("r", value);
        }
        [Attribute("fx", SvgAttributes.SvgNamespace)]
        public string? FocalX
        {
            get => GetAttribute("fx");
            set => SetAttribute("fx", value);
        }

        [Attribute("fy", SvgAttributes.SvgNamespace)]
        public string? FocalY
        {
            get => GetAttribute("fy");
            set => SetAttribute("fy", value);
        }

        [Attribute("gradientUnits", SvgAttributes.SvgNamespace)]
        public string? GradientUnits
        {
            get => GetAttribute("gradientUnits");
            set => SetAttribute("gradientUnits", value);
        }

        [Attribute("gradientTransform", SvgAttributes.SvgNamespace)]
        public string? GradientTransform
        {
            get => GetAttribute("gradientTransform");
            set => SetAttribute("gradientTransform", value);
        }

        [Attribute("spreadMethod", SvgAttributes.SvgNamespace)]
        public string? SpreadMethod
        {
            get => GetAttribute("spreadMethod");
            set => SetAttribute("spreadMethod", value);
        }

        [Attribute("href", SvgAttributes.XLinkNamespace)]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (CenterX != null)
            {
                write($"{indent}{nameof(CenterX)}: \"{CenterX}\"");
            }
            if (CenterY != null)
            {
                write($"{indent}{nameof(CenterY)}: \"{CenterY}\"");
            }
            if (Radius != null)
            {
                write($"{indent}{nameof(Radius)}: \"{Radius}\"");
            }
            if (FocalX != null)
            {
                write($"{indent}{nameof(FocalX)}: \"{FocalX}\"");
            }
            if (FocalY != null)
            {
                write($"{indent}{nameof(FocalY)}: \"{FocalY}\"");
            }
            if (GradientUnits != null)
            {
                write($"{indent}{nameof(GradientUnits)}: \"{GradientUnits}\"");
            }
            if (GradientTransform != null)
            {
                write($"{indent}{nameof(GradientTransform)}: \"{GradientTransform}\"");
            }
            if (SpreadMethod != null)
            {
                write($"{indent}{nameof(SpreadMethod)}: \"{SpreadMethod}\"");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
