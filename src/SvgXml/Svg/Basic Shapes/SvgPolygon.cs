using System;
using Xml;

namespace Svg
{
    [Element("polygon")]
    public class SvgPolygon : SvgMarkerElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
        [Attribute("points")]
        public string? Points
        {
            get => GetAttribute("points");
            set => SetAttribute("points", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Points != null)
            {
                Console.WriteLine($"{indent}{nameof(Points)}='{Points}'");
            }
        }
    }
}
