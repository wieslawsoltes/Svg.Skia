using System;
using Xml;

namespace Svg
{
    [Element("font")]
    public class SvgFont : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("horiz-origin-x", SvgNamespace)]
        public string? HorizOriginX
        {
            get => this.GetAttribute("horiz-origin-x", false, "0");
            set => this.SetAttribute("horiz-origin-x", value);
        }

        [Attribute("horiz-origin-y", SvgNamespace)]
        public string? HorizOriginY
        {
            get => this.GetAttribute("horiz-origin-y", false, "0");
            set => this.SetAttribute("horiz-origin-y", value);
        }

        [Attribute("horiz-adv-x", SvgNamespace)]
        public string? HorizAdvX
        {
            get => this.GetAttribute("horiz-adv-x", false, null);
            set => this.SetAttribute("horiz-adv-x", value);
        }

        [Attribute("vert-origin-x", SvgNamespace)]
        public string? VertOriginX
        {
            get => this.GetAttribute("vert-origin-x", false, null); // TODO:
            set => this.SetAttribute("vert-origin-x", value);
        }

        [Attribute("vert-origin-y", SvgNamespace)]
        public string? VertOriginY
        {
            get => this.GetAttribute("vert-origin-y", false, null); // TODO:
            set => this.SetAttribute("vert-origin-y", value);
        }

        [Attribute("vert-adv-y", SvgNamespace)]
        public string? VertAdvY
        {
            get => this.GetAttribute("vert-adv-y", false, null); // TODO:
            set => this.SetAttribute("vert-adv-y", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "horiz-origin-x":
                    HorizOriginX = value;
                    break;
                case "horiz-origin-y":
                    HorizOriginY = value;
                    break;
                case "horiz-adv-x":
                    HorizAdvX = value;
                    break;
                case "vert-origin-x":
                    VertOriginX = value;
                    break;
                case "vert-origin-y":
                    VertOriginY = value;
                    break;
                case "vert-adv-y":
                    VertAdvY = value;
                    break;
            }
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
