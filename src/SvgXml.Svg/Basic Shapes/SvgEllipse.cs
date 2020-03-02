using System;
using Xml;

namespace Svg
{
    [Element("ellipse")]
    public class SvgEllipse : SvgPathBasedElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
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

        [Attribute("rx", SvgNamespace)]
        public string? RadiusX
        {
            get => this.GetAttribute("rx");
            set => this.SetAttribute("rx", value);
        }

        [Attribute("ry", SvgNamespace)]
        public string? RadiusY
        {
            get => this.GetAttribute("ry");
            set => this.SetAttribute("ry", value);
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
            if (RadiusX != null)
            {
                write($"{indent}{nameof(RadiusX)}: \"{RadiusX}\"");
            }
            if (RadiusY != null)
            {
                write($"{indent}{nameof(RadiusY)}: \"{RadiusY}\"");
            }
        }
    }
}
