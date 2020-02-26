using System;
using Xml;

namespace Svg
{
    [Element("rect")]
    public class SvgRectangle : SvgPathBasedElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
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

        [Attribute("width", SvgAttributes.SvgNamespace)]
        public string? Width
        {
            get => GetAttribute("width");
            set => SetAttribute("width", value);
        }

        [Attribute("height", SvgAttributes.SvgNamespace)]
        public string? Height
        {
            get => GetAttribute("height");
            set => SetAttribute("height", value);
        }

        [Attribute("rx", SvgAttributes.SvgNamespace)]
        public string? CornerRadiusX
        {
            get => GetAttribute("rx");
            set => SetAttribute("rx", value);
        }

        [Attribute("ry", SvgAttributes.SvgNamespace)]
        public string? CornerRadiusY
        {
            get => GetAttribute("ry");
            set => SetAttribute("ry", value);
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
            if (Width != null)
            {
                write($"{indent}{nameof(Width)}: \"{Width}\"");
            }
            if (Height != null)
            {
                write($"{indent}{nameof(Height)}: \"{Height}\"");
            }
            if (CornerRadiusX != null)
            {
                write($"{indent}{nameof(CornerRadiusX)}: \"{CornerRadiusX}\"");
            }
            if (CornerRadiusY != null)
            {
                write($"{indent}{nameof(CornerRadiusY)}: \"{CornerRadiusY}\"");
            }
        }
    }
}
