using System;
using Xml;

namespace Svg
{
    [Element("clipPath")]
    public class SvgClipPath : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
        // ISvgTransformableAttributes

        [Attribute("transform", SvgElement.SvgNamespace)]
        public string? Transform
        {
            get => this.GetAttribute("transform", false, null);
            set => this.SetAttribute("transform", value);
        }

        // SvgClipPath

        [Attribute("clipPathUnits", SvgNamespace)]
        public string? ClipPathUnits
        {
            get => this.GetAttribute("clipPathUnits");
            set => this.SetAttribute("clipPathUnits", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (ClipPathUnits != null)
            {
                write($"{indent}{nameof(ClipPathUnits)}: \"{ClipPathUnits}\"");
            }
        }
    }
}
