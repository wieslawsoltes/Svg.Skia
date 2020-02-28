using System;
using Xml;

namespace Svg
{
    [Element("polygon")]
    public class SvgPolygon : SvgMarkerElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
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
