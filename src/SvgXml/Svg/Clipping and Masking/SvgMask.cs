using System;
using Xml;

namespace Svg
{
    [Element("mask")]
    public class SvgMask : SvgElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y");
            set => this.SetAttribute("y", value);
        }

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => this.GetAttribute("width");
            set => this.SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => this.GetAttribute("height");
            set => this.SetAttribute("height", value);
        }

        [Attribute("maskUnits", SvgNamespace)]
        public string? MaskUnits
        {
            get => this.GetAttribute("maskUnits");
            set => this.SetAttribute("maskUnits", value);
        }

        [Attribute("maskContentUnits", SvgNamespace)]
        public string? MaskContentUnits
        {
            get => this.GetAttribute("maskContentUnits");
            set => this.SetAttribute("maskContentUnits", value);
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
