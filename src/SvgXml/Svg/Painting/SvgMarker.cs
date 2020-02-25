using System;
using Xml;

namespace Svg
{
    [Element("marker")]
    public class SvgMarker : SvgPathBasedElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("viewBox")]
        public string? ViewBox
        {
            get => GetAttribute("viewBox");
            set => SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio")]
        public string? AspectRatio
        {
            get => GetAttribute("preserveAspectRatio");
            set => SetAttribute("preserveAspectRatio", value);
        }

        [Attribute("refX")]
        public string? RefX
        {
            get => GetAttribute("refX");
            set => SetAttribute("refX", value);
        }

        [Attribute("refY")]
        public string? RefY
        {
            get => GetAttribute("refY");
            set => SetAttribute("refY", value);
        }

        [Attribute("markerUnits")]
        public string? MarkerUnits
        {
            get => GetAttribute("markerUnits");
            set => SetAttribute("markerUnits", value);
        }

        [Attribute("markerWidth")]
        public string? MarkerWidth
        {
            get => GetAttribute("markerWidth");
            set => SetAttribute("markerWidth", value);
        }

        [Attribute("markerHeight")]
        public string? MarkerHeight
        {
            get => GetAttribute("markerHeight");
            set => SetAttribute("markerHeight", value);
        }

        [Attribute("orient")]
        public string? Orient
        {
            get => GetAttribute("orient");
            set => SetAttribute("orient", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (ViewBox != null)
            {
                Console.WriteLine($"{indent}{nameof(ViewBox)}='{ViewBox}'");
            }
            if (AspectRatio != null)
            {
                Console.WriteLine($"{indent}{nameof(AspectRatio)}='{AspectRatio}'");
            }
            if (RefX != null)
            {
                Console.WriteLine($"{indent}{nameof(RefX)}='{RefX}'");
            }
            if (RefY != null)
            {
                Console.WriteLine($"{indent}{nameof(RefY)}='{RefY}'");
            }
            if (MarkerUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(MarkerUnits)}='{MarkerUnits}'");
            }
            if (MarkerWidth != null)
            {
                Console.WriteLine($"{indent}{nameof(MarkerWidth)}='{MarkerWidth}'");
            }
            if (MarkerHeight != null)
            {
                Console.WriteLine($"{indent}{nameof(MarkerHeight)}='{MarkerHeight}'");
            }
            if (Orient != null)
            {
                Console.WriteLine($"{indent}{nameof(Orient)}='{Orient}'");
            }
        }
    }
}
