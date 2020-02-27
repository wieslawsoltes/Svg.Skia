using System;
using Xml;

namespace Svg
{
    [Element("ellipse")]
    public class SvgEllipse : SvgPathBasedElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        [Attribute("cx", SvgElement.SvgNamespace)]
        public string? CenterX
        {
            get => GetAttribute("cx");
            set => SetAttribute("cx", value);
        }

        [Attribute("cy", SvgElement.SvgNamespace)]
        public string? CenterY
        {
            get => GetAttribute("cy");
            set => SetAttribute("cy", value);
        }

        [Attribute("rx", SvgElement.SvgNamespace)]
        public string? RadiusX
        {
            get => GetAttribute("rx");
            set => SetAttribute("rx", value);
        }

        [Attribute("ry", SvgElement.SvgNamespace)]
        public string? RadiusY
        {
            get => GetAttribute("ry");
            set => SetAttribute("ry", value);
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
