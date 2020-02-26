using System;
using Xml;

namespace Svg
{
    [Element("line")]
    public class SvgLine : SvgMarkerElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        [Attribute("x1")]
        public string? StartX
        {
            get => GetAttribute("x1");
            set => SetAttribute("x1", value);
        }

        [Attribute("y1")]
        public string? StartY
        {
            get => GetAttribute("y1");
            set => SetAttribute("y1", value);
        }

        [Attribute("x2")]
        public string? EndX
        {
            get => GetAttribute("x2");
            set => SetAttribute("x2", value);
        }

        [Attribute("y2")]
        public string? EndY
        {
            get => GetAttribute("y2");
            set => SetAttribute("y2", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (StartX != null)
            {
                write($"{indent}{nameof(StartX)}: \"{StartX}\"");
            }
            if (StartY != null)
            {
                write($"{indent}{nameof(StartY)}: \"{StartY}\"");
            }
            if (EndX != null)
            {
                write($"{indent}{nameof(EndX)}: \"{EndX}\"");
            }
            if (EndY != null)
            {
                write($"{indent}{nameof(EndY)}: \"{EndY}\"");
            }
        }
    }
}
