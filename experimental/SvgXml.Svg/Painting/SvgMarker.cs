using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Painting
{
    [Element("marker")]
    public class SvgMarker : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("viewBox", SvgNamespace)]
        public string? ViewBox
        {
            get => this.GetAttribute("viewBox", false, null);
            set => this.SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio", SvgNamespace)]
        public string? AspectRatio
        {
            get => this.GetAttribute("preserveAspectRatio", false, "xMidYMid meet");
            set => this.SetAttribute("preserveAspectRatio", value);
        }

        [Attribute("refX", SvgNamespace)]
        public string? RefX
        {
            get => this.GetAttribute("refX", false, "0");
            set => this.SetAttribute("refX", value);
        }

        [Attribute("refY", SvgNamespace)]
        public string? RefY
        {
            get => this.GetAttribute("refY", false, "0");
            set => this.SetAttribute("refY", value);
        }

        [Attribute("markerUnits", SvgNamespace)]
        public string? MarkerUnits
        {
            get => this.GetAttribute("markerUnits", false, "strokeWidth");
            set => this.SetAttribute("markerUnits", value);
        }

        [Attribute("markerWidth", SvgNamespace)]
        public string? MarkerWidth
        {
            get => this.GetAttribute("markerWidth", false, "3");
            set => this.SetAttribute("markerWidth", value);
        }

        [Attribute("markerHeight", SvgNamespace)]
        public string? MarkerHeight
        {
            get => this.GetAttribute("markerHeight", false, "3");
            set => this.SetAttribute("markerHeight", value);
        }

        [Attribute("orient", SvgNamespace)]
        public string? Orient
        {
            get => this.GetAttribute("orient", false, "0");
            set => this.SetAttribute("orient", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "viewBox":
                    ViewBox = value;
                    break;
                case "preserveAspectRatio":
                    AspectRatio = value;
                    break;
                case "refX":
                    RefX = value;
                    break;
                case "refY":
                    RefY = value;
                    break;
                case "markerUnits":
                    MarkerUnits = value;
                    break;
                case "markerWidth":
                    MarkerWidth = value;
                    break;
                case "markerHeight":
                    MarkerHeight = value;
                    break;
                case "orient":
                    Orient = value;
                    break;
            }
        }
    }
}
