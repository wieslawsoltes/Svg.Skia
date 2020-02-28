using System;
using Xml;

namespace Svg
{
    [Element("linearGradient")]
    public class SvgLinearGradientServer : SvgGradientServer,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("x1", SvgNamespace)]
        public string? X1
        {
            get => this.GetAttribute("x1");
            set => this.SetAttribute("x1", value);
        }

        [Attribute("y1", SvgNamespace)]
        public string? Y1
        {
            get => this.GetAttribute("y1");
            set => this.SetAttribute("y1", value);
        }

        [Attribute("x2", SvgNamespace)]
        public string? X2
        {
            get => this.GetAttribute("x2");
            set => this.SetAttribute("x2", value);
        }

        [Attribute("y2", SvgNamespace)]
        public string? Y2
        {
            get => this.GetAttribute("y2");
            set => this.SetAttribute("y2", value);
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

            if (X1 != null)
            {
                write($"{indent}{nameof(X1)}: \"{X1}\"");
            }
            if (Y1 != null)
            {
                write($"{indent}{nameof(Y1)}: \"{Y1}\"");
            }
            if (X2 != null)
            {
                write($"{indent}{nameof(X2)}: \"{X2}\"");
            }
            if (Y2 != null)
            {
                write($"{indent}{nameof(Y2)}: \"{Y2}\"");
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
