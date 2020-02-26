using System;
using Xml;

namespace Svg
{
    [Element("linearGradient")]
    public class SvgLinearGradientServer : SvgGradientServer, ISvgPresentationAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        [Attribute("x1")]
        public string? X1
        {
            get => GetAttribute("x1");
            set => SetAttribute("x1", value);
        }

        [Attribute("y1")]
        public string? Y1
        {
            get => GetAttribute("y1");
            set => SetAttribute("y1", value);
        }

        [Attribute("x2")]
        public string? X2
        {
            get => GetAttribute("x2");
            set => SetAttribute("x2", value);
        }

        [Attribute("y2")]
        public string? Y2
        {
            get => GetAttribute("y2");
            set => SetAttribute("y2", value);
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

            if (X1 != null)
            {
                Console.WriteLine($"{indent}{nameof(X1)}: \"{X1}\"");
            }
            if (Y1 != null)
            {
                Console.WriteLine($"{indent}{nameof(Y1)}: \"{Y1}\"");
            }
            if (X2 != null)
            {
                Console.WriteLine($"{indent}{nameof(X2)}: \"{X2}\"");
            }
            if (Y2 != null)
            {
                Console.WriteLine($"{indent}{nameof(Y2)}: \"{Y2}\"");
            }
            if (GradientUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(GradientUnits)}: \"{GradientUnits}\"");
            }
            if (GradientTransform != null)
            {
                Console.WriteLine($"{indent}{nameof(GradientTransform)}: \"{GradientTransform}\"");
            }
            if (SpreadMethod != null)
            {
                Console.WriteLine($"{indent}{nameof(SpreadMethod)}: \"{SpreadMethod}\"");
            }
            if (Href != null)
            {
                Console.WriteLine($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
