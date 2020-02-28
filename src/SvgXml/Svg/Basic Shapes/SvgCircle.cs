using System;
using Xml;

namespace Svg
{
    [Element("circle")]
    public class SvgCircle : SvgPathBasedElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        [Attribute("cx", SvgNamespace)]
        public string? CenterX
        {
            get => GetAttribute("cx");
            set => SetAttribute("cx", value);
        }

        [Attribute("cy", SvgNamespace)]
        public string? CenterY
        {
            get => GetAttribute("cy");
            set => SetAttribute("cy", value);
        }

        [Attribute("r", SvgNamespace)]
        public string? Radius
        {
            get => GetAttribute("r");
            set => SetAttribute("r", value);
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
        }
    }
}
