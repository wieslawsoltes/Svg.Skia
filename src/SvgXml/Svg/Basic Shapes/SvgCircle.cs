using System;
using Xml;

namespace Svg
{
    [Element("circle")]
    public class SvgCircle : SvgPathBasedElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
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
        }
    }
}
