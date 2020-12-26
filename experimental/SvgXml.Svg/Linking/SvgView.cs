using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Linking
{
    [Element("view")]
    public class SvgView : SvgElement,
        ISvgCommonAttributes,
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

        [Attribute("zoomAndPan", SvgNamespace)]
        public string? ZoomAndPan
        {
            get => this.GetAttribute("zoomAndPan", false, "magnify");
            set => this.SetAttribute("zoomAndPan", value);
        }

        [Attribute("viewTarget", SvgNamespace)]
        public string? ViewTarget
        {
            get => this.GetAttribute("viewTarget", false, null);
            set => this.SetAttribute("viewTarget", value);
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
                case "zoomAndPan":
                    ZoomAndPan = value;
                    break;
                case "viewTarget":
                    ViewTarget = value;
                    break;
            }
        }
    }
}
