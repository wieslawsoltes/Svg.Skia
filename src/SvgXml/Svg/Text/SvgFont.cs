using System;
using Xml;

namespace Svg
{
    [Element("font")]
    public class SvgFont : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        [Attribute("horiz-origin-x", SvgElement.SvgNamespace)]
        public string? HorizOriginX
        {
            get => GetAttribute("horiz-origin-x");
            set => SetAttribute("horiz-origin-x", value);
        }

        [Attribute("horiz-origin-y", SvgElement.SvgNamespace)]
        public string? HorizOriginY
        {
            get => GetAttribute("horiz-origin-y");
            set => SetAttribute("horiz-origin-y", value);
        }

        [Attribute("horiz-adv-x", SvgElement.SvgNamespace)]
        public string? HorizAdvX
        {
            get => GetAttribute("horiz-adv-x");
            set => SetAttribute("horiz-adv-x", value);
        }

        [Attribute("vert-origin-x", SvgElement.SvgNamespace)]
        public string? VertOriginX
        {
            get => GetAttribute("vert-origin-x");
            set => SetAttribute("vert-origin-x", value);
        }

        [Attribute("vert-origin-y", SvgElement.SvgNamespace)]
        public string? VertOriginY
        {
            get => GetAttribute("vert-origin-y");
            set => SetAttribute("vert-origin-y", value);
        }

        [Attribute("vert-adv-y", SvgElement.SvgNamespace)]
        public string? VertAdvY
        {
            get => GetAttribute("vert-adv-y");
            set => SetAttribute("vert-adv-y", value);
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
