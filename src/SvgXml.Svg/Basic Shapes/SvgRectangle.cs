using System;
using Xml;

namespace Svg
{
    [Element("rect")]
    public class SvgRectangle : SvgPathBasedElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
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

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => this.GetAttribute("width");
            set => this.SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => this.GetAttribute("height");
            set => this.SetAttribute("height", value);
        }

        [Attribute("rx", SvgNamespace)]
        public string? CornerRadiusX
        {
            get => this.GetAttribute("rx");
            set => this.SetAttribute("rx", value);
        }

        [Attribute("ry", SvgNamespace)]
        public string? CornerRadiusY
        {
            get => this.GetAttribute("ry");
            set => this.SetAttribute("ry", value);
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
