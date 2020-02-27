using System;
using Xml;

namespace Svg
{
    [Element("polygon")]
    public class SvgPolygon : SvgMarkerElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        [Attribute("points", SvgElement.SvgNamespace)]
        public string? Points
        {
            get => GetAttribute("points");
            set => SetAttribute("points", value);
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
