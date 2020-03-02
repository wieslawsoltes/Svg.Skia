using System;
using Xml;

namespace Svg
{
    [Element("circle")]
    public class SvgCircle : SvgPathBasedElement,
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

        [Attribute("r", SvgNamespace)]
        public string? Radius
        {
            get => this.GetAttribute("r");
            set => this.SetAttribute("r", value);
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
