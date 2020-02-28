using System;
using Xml;

namespace Svg
{
    [Element("radialGradient")]
    public class SvgRadialGradientServer : SvgGradientServer,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("cx", SvgNamespace)]
        public string? CenterX
        {
            get => this.GetAttribute("cx");
            set => this.SetAttribute("cx", value);
        }

        [Attribute("cy", SvgNamespace)]
        public string? CenterY
        {
            get => this.GetAttribute("cy");
            set => this.SetAttribute("cy", value);
        }

        [Attribute("r", SvgNamespace)]
        public string? Radius
        {
            get => this.GetAttribute("r");
            set => this.SetAttribute("r", value);
        }
        [Attribute("fx", SvgNamespace)]
        public string? FocalX
        {
            get => this.GetAttribute("fx");
            set => this.SetAttribute("fx", value);
        }

        [Attribute("fy", SvgNamespace)]
        public string? FocalY
        {
            get => this.GetAttribute("fy");
            set => this.SetAttribute("fy", value);
        }

        [Attribute("gradientUnits", SvgNamespace)]
        public string? GradientUnits
        {
            get => this.GetAttribute("gradientUnits");
            set => this.SetAttribute("gradientUnits", value);
        }

        [Attribute("gradientTransform", SvgNamespace)]
        public string? GradientTransform
        {
            get => this.GetAttribute("gradientTransform");
            set => this.SetAttribute("gradientTransform", value);
        }

        [Attribute("spreadMethod", SvgNamespace)]
        public string? SpreadMethod
        {
            get => this.GetAttribute("spreadMethod");
            set => this.SetAttribute("spreadMethod", value);
        }

        [Attribute("href", XLinkNamespace)]
        public string? Href
        {
            get => this.GetAttribute("href");
            set => this.SetAttribute("href", value);
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
