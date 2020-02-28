using System;
using Xml;

namespace Svg
{
    [Element("mask")]
    public class SvgMask : SvgElement,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
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

        [Attribute("maskUnits", SvgNamespace)]
        public string? MaskUnits
        {
            get => GetAttribute("maskUnits");
            set => SetAttribute("maskUnits", value);
        }

        [Attribute("maskContentUnits", SvgNamespace)]
        public string? MaskContentUnits
        {
            get => GetAttribute("maskContentUnits");
            set => SetAttribute("maskContentUnits", value);
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
            if (MaskUnits != null)
            {
                write($"{indent}{nameof(MaskUnits)}: \"{MaskUnits}\"");
            }
            if (MaskContentUnits != null)
            {
                write($"{indent}{nameof(MaskContentUnits)}: \"{MaskContentUnits}\"");
            }
        }
    }
}
