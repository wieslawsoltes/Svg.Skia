using System;
using Xml;

namespace Svg
{
    [Element("view")]
    public class SvgView : SvgElement,
        ISvgResourcesAttributes
    {
        [Attribute("viewBox", SvgNamespace)]
        public string? ViewBox
        {
            get => GetAttribute("viewBox");
            set => SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio", SvgNamespace)]
        public string? AspectRatio
        {
            get => GetAttribute("preserveAspectRatio");
            set => SetAttribute("preserveAspectRatio", value);
        }

        [Attribute("zoomAndPan", SvgNamespace)]
        public string? ZoomAndPan
        {
            get => GetAttribute("zoomAndPan");
            set => SetAttribute("zoomAndPan", value);
        }

        [Attribute("viewTarget", SvgNamespace)]
        public string? ViewTarget
        {
            get => GetAttribute("viewTarget");
            set => SetAttribute("viewTarget", value);
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
