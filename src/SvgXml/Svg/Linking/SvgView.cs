using System;
using Xml;

namespace Svg
{
    [Element("view")]
    public class SvgView : SvgElement,
        ISvgCommonAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("viewBox", SvgNamespace)]
        public string? ViewBox
        {
            get => this.GetAttribute("viewBox");
            set => this.SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio", SvgNamespace)]
        public string? AspectRatio
        {
            get => this.GetAttribute("preserveAspectRatio");
            set => this.SetAttribute("preserveAspectRatio", value);
        }

        [Attribute("zoomAndPan", SvgNamespace)]
        public string? ZoomAndPan
        {
            get => this.GetAttribute("zoomAndPan");
            set => this.SetAttribute("zoomAndPan", value);
        }

        [Attribute("viewTarget", SvgNamespace)]
        public string? ViewTarget
        {
            get => this.GetAttribute("viewTarget");
            set => this.SetAttribute("viewTarget", value);
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
            if (ZoomAndPan != null)
            {
                write($"{indent}{nameof(ZoomAndPan)}: \"{ZoomAndPan}\"");
            }
            if (ViewTarget != null)
            {
                write($"{indent}{nameof(ViewTarget)}: \"{ViewTarget}\"");
            }
        }
    }
}
