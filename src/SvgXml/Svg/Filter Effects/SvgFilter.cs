using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("filter")]
    public class SvgFilter : SvgElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => GetAttribute("width");
            set => SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => GetAttribute("height");
            set => SetAttribute("height", value);
        }

        [Attribute("filterRes", SvgNamespace)]
        public string? FilterRes
        {
            get => GetAttribute("filterRes");
            set => SetAttribute("filterRes", value);
        }

        [Attribute("filterUnits", SvgNamespace)]
        public string? FilterUnits
        {
            get => GetAttribute("filterUnits");
            set => SetAttribute("filterUnits", value);
        }

        [Attribute("primitiveUnits", SvgNamespace)]
        public string? PrimitiveUnits
        {
            get => GetAttribute("primitiveUnits");
            set => SetAttribute("primitiveUnits", value);
        }

        [Attribute("href", XLinkNamespace)]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
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
            if (FilterRes != null)
            {
                write($"{indent}{nameof(FilterRes)}: \"{FilterRes}\"");
            }
            if (FilterUnits != null)
            {
                write($"{indent}{nameof(FilterUnits)}: \"{FilterUnits}\"");
            }
            if (PrimitiveUnits != null)
            {
                write($"{indent}{nameof(PrimitiveUnits)}: \"{PrimitiveUnits}\"");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
