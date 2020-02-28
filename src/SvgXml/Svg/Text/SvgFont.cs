using System;
using Xml;

namespace Svg
{
    [Element("font")]
    public class SvgFont : SvgElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("horiz-origin-x", SvgNamespace)]
        public string? HorizOriginX
        {
            get => this.GetAttribute("horiz-origin-x");
            set => this.SetAttribute("horiz-origin-x", value);
        }

        [Attribute("horiz-origin-y", SvgNamespace)]
        public string? HorizOriginY
        {
            get => this.GetAttribute("horiz-origin-y");
            set => this.SetAttribute("horiz-origin-y", value);
        }

        [Attribute("horiz-adv-x", SvgNamespace)]
        public string? HorizAdvX
        {
            get => this.GetAttribute("horiz-adv-x");
            set => this.SetAttribute("horiz-adv-x", value);
        }

        [Attribute("vert-origin-x", SvgNamespace)]
        public string? VertOriginX
        {
            get => this.GetAttribute("vert-origin-x");
            set => this.SetAttribute("vert-origin-x", value);
        }

        [Attribute("vert-origin-y", SvgNamespace)]
        public string? VertOriginY
        {
            get => this.GetAttribute("vert-origin-y");
            set => this.SetAttribute("vert-origin-y", value);
        }

        [Attribute("vert-adv-y", SvgNamespace)]
        public string? VertAdvY
        {
            get => this.GetAttribute("vert-adv-y");
            set => this.SetAttribute("vert-adv-y", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (HorizOriginX != null)
            {
                write($"{indent}{nameof(HorizOriginX)}: \"{HorizOriginX}\"");
            }
            if (HorizOriginY != null)
            {
                write($"{indent}{nameof(HorizOriginY)}: \"{HorizOriginY}\"");
            }
            if (HorizAdvX != null)
            {
                write($"{indent}{nameof(HorizAdvX)}: \"{HorizAdvX}\"");
            }
            if (VertOriginX != null)
            {
                write($"{indent}{nameof(VertOriginX)}: \"{VertOriginX}\"");
            }
            if (VertOriginY != null)
            {
                write($"{indent}{nameof(VertOriginY)}: \"{VertOriginY}\"");
            }
            if (VertAdvY != null)
            {
                write($"{indent}{nameof(VertAdvY)}: \"{VertAdvY}\"");
            }
        }
    }
}
