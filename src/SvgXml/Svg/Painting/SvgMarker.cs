using System;
using Xml;

namespace Svg
{
    [Element("marker")]
    public class SvgMarker : SvgPathBasedElement, ISvgPresentationAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        [Attribute("viewBox", SvgAttributes.SvgNamespace)]
        public string? ViewBox
        {
            get => GetAttribute("viewBox");
            set => SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio", SvgAttributes.SvgNamespace)]
        public string? AspectRatio
        {
            get => GetAttribute("preserveAspectRatio");
            set => SetAttribute("preserveAspectRatio", value);
        }

        [Attribute("refX", SvgAttributes.SvgNamespace)]
        public string? RefX
        {
            get => GetAttribute("refX");
            set => SetAttribute("refX", value);
        }

        [Attribute("refY", SvgAttributes.SvgNamespace)]
        public string? RefY
        {
            get => GetAttribute("refY");
            set => SetAttribute("refY", value);
        }

        [Attribute("markerUnits", SvgAttributes.SvgNamespace)]
        public string? MarkerUnits
        {
            get => GetAttribute("markerUnits");
            set => SetAttribute("markerUnits", value);
        }

        [Attribute("markerWidth", SvgAttributes.SvgNamespace)]
        public string? MarkerWidth
        {
            get => GetAttribute("markerWidth");
            set => SetAttribute("markerWidth", value);
        }

        [Attribute("markerHeight", SvgAttributes.SvgNamespace)]
        public string? MarkerHeight
        {
            get => GetAttribute("markerHeight");
            set => SetAttribute("markerHeight", value);
        }

        [Attribute("orient", SvgAttributes.SvgNamespace)]
        public string? Orient
        {
            get => GetAttribute("orient");
            set => SetAttribute("orient", value);
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
            if (RefX != null)
            {
                write($"{indent}{nameof(RefX)}: \"{RefX}\"");
            }
            if (RefY != null)
            {
                write($"{indent}{nameof(RefY)}: \"{RefY}\"");
            }
            if (MarkerUnits != null)
            {
                write($"{indent}{nameof(MarkerUnits)}: \"{MarkerUnits}\"");
            }
            if (MarkerWidth != null)
            {
                write($"{indent}{nameof(MarkerWidth)}: \"{MarkerWidth}\"");
            }
            if (MarkerHeight != null)
            {
                write($"{indent}{nameof(MarkerHeight)}: \"{MarkerHeight}\"");
            }
            if (Orient != null)
            {
                write($"{indent}{nameof(Orient)}: \"{Orient}\"");
            }
        }
    }
}
