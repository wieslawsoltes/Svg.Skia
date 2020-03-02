using System;
using Xml;

namespace Svg
{
    [Element("radialGradient")]
    public class SvgRadialGradientServer : SvgGradientServer,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("cx", SvgNamespace)]
        public string? CenterX
        {
            get => this.GetAttribute("cx", false, "50%");
            set => this.SetAttribute("cx", value);
        }

        [Attribute("cy", SvgNamespace)]
        public string? CenterY
        {
            get => this.GetAttribute("cy", false, "50%");
            set => this.SetAttribute("cy", value);
        }

        [Attribute("r", SvgNamespace)]
        public string? Radius
        {
            get => this.GetAttribute("r", false, "50%");
            set => this.SetAttribute("r", value);
        }
        [Attribute("fx", SvgNamespace)]
        public string? FocalX
        {
            get => this.GetAttribute("fx", false, null);
            set => this.SetAttribute("fx", value);
        }

        [Attribute("fy", SvgNamespace)]
        public string? FocalY
        {
            get => this.GetAttribute("fy", false, null);
            set => this.SetAttribute("fy", value);
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
            if (FocalX != null)
            {
                write($"{indent}{nameof(FocalX)}: \"{FocalX}\"");
            }
            if (FocalY != null)
            {
                write($"{indent}{nameof(FocalY)}: \"{FocalY}\"");
            }
        }
    }
}
