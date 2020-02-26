using System;
using Xml;

namespace Svg
{
    [Element("ellipse")]
    public class SvgEllipse : SvgPathBasedElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
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

        [Attribute("rx")]
        public string? RadiusX
        {
            get => GetAttribute("rx");
            set => SetAttribute("rx", value);
        }

        [Attribute("ry")]
        public string? RadiusY
        {
            get => GetAttribute("ry");
            set => SetAttribute("ry", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (CenterX != null)
            {
                Console.WriteLine($"{indent}{nameof(CenterX)}: \"{CenterX}\"");
            }
            if (CenterY != null)
            {
                Console.WriteLine($"{indent}{nameof(CenterY)}: \"{CenterY}\"");
            }
            if (RadiusX != null)
            {
                Console.WriteLine($"{indent}{nameof(RadiusX)}: \"{RadiusX}\"");
            }
            if (RadiusY != null)
            {
                Console.WriteLine($"{indent}{nameof(RadiusY)}: \"{RadiusY}\"");
            }
        }
    }
}
