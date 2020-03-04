using System;
using Xml;

namespace Svg
{
    [Element("polygon")]
    public class SvgPolygon : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
        // ISvgTransformableAttributes

        [Attribute("transform", SvgNamespace)]
        public string? Transform
        {
            get => this.GetAttribute("transform", false, null);
            set => this.SetAttribute("transform", value);
        }

        // SvgPolygon

        [Attribute("points", SvgNamespace)]
        public string? Points
        {
            get => this.GetAttribute("points");
            set => this.SetAttribute("points", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Points != null)
            {
                write($"{indent}{nameof(Points)}: \"{Points}\"");
            }
        }
    }
}
