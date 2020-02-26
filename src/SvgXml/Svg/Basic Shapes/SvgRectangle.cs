using System;
using Xml;

namespace Svg
{
    [Element("rect")]
    public class SvgRectangle : SvgPathBasedElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
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

        [Attribute("width")]
        public string? Width
        {
            get => GetAttribute("width");
            set => SetAttribute("width", value);
        }

        [Attribute("height")]
        public string? Height
        {
            get => GetAttribute("height");
            set => SetAttribute("height", value);
        }

        [Attribute("rx")]
        public string? CornerRadiusX
        {
            get => GetAttribute("rx");
            set => SetAttribute("rx", value);
        }

        [Attribute("ry")]
        public string? CornerRadiusY
        {
            get => GetAttribute("ry");
            set => SetAttribute("ry", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (X != null)
            {
                Console.WriteLine($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                Console.WriteLine($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Width != null)
            {
                Console.WriteLine($"{indent}{nameof(Width)}: \"{Width}\"");
            }
            if (Height != null)
            {
                Console.WriteLine($"{indent}{nameof(Height)}: \"{Height}\"");
            }
            if (CornerRadiusX != null)
            {
                Console.WriteLine($"{indent}{nameof(CornerRadiusX)}: \"{CornerRadiusX}\"");
            }
            if (CornerRadiusY != null)
            {
                Console.WriteLine($"{indent}{nameof(CornerRadiusY)}: \"{CornerRadiusY}\"");
            }
        }
    }
}
