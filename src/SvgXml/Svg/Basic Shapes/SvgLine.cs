using System;
using Xml;

namespace Svg
{
    [Element("line")]
    public class SvgLine: SvgMarkerElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
        [Attribute("x1", SvgNamespace)]
        public string? StartX
        {
            get => this.GetAttribute("x1");
            set => this.SetAttribute("x1", value);
        }

        [Attribute("y1", SvgNamespace)]
        public string? StartY
        {
            get => this.GetAttribute("y1");
            set => this.SetAttribute("y1", value);
        }

        [Attribute("x2", SvgNamespace)]
        public string? EndX
        {
            get => this.GetAttribute("x2");
            set => this.SetAttribute("x2", value);
        }

        [Attribute("y2", SvgNamespace)]
        public string? EndY
        {
            get => this.GetAttribute("y2");
            set => this.SetAttribute("y2", value);
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
