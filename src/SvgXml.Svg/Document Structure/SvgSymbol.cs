using System;
using Xml;

namespace Svg
{
    [Element("symbol")]
    public class SvgSymbol : SvgVisualElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("viewBox", SvgNamespace)]
        public string? ViewBox
        {
            get => this.GetAttribute("viewBox");
            set => this.SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio", SvgNamespace)]
        public string? AspectRatio
        {
            get => this.GetAttribute("preserveAspectRatio");
            set => this.SetAttribute("preserveAspectRatio", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (ViewBox != null)
            {
                write($"{indent}{nameof(ViewBox)}: \"{ViewBox}\"");
            }
            if (AspectRatio != null)
            {
                write($"{indent}{nameof(AspectRatio)}: \"{AspectRatio}\"");
            }
        }
    }
}
