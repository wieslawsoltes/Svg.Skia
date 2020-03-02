using System;
using Xml;

namespace Svg
{
    [Element("svg")]
    public class SvgFragment : SvgElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x", false, "0");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y", false, "0");
            set => this.SetAttribute("y", value);
        }

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => this.GetAttribute("width", false, "100%");
            set => this.SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => this.GetAttribute("height", false, "100%");
            set => this.SetAttribute("height", value);
        }

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

        [Attribute("version", SvgNamespace)]
        public string? Version
        {
            get => this.GetAttribute("version", false, "1.1");
            set => this.SetAttribute("version", value);
        }

        [Attribute("baseProfile", SvgNamespace)]
        public string? BaseProfile
        {
            get => this.GetAttribute("baseProfile", false, "none");
            set => this.SetAttribute("baseProfile", value);
        }

        [Attribute("contentScriptType", SvgNamespace)]
        public string? ContentScriptType
        {
            get => this.GetAttribute("contentScriptType", false, "application/ecmascript");
            set => this.SetAttribute("contentScriptType", value);
        }

        [Attribute("contentStyleType", SvgNamespace)]
        public string? ContentStyleType
        {
            get => this.GetAttribute("contentStyleType", false, "text/css");
            set => this.SetAttribute("contentStyleType", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (X != null)
            {
                write($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                write($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Width != null)
            {
                write($"{indent}{nameof(Width)}: \"{Width}\"");
            }
            if (Height != null)
            {
                write($"{indent}{nameof(Height)}: \"{Height}\"");
            }
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
            if (Version != null)
            {
                write($"{indent}{nameof(Version)}: \"{Version}\"");
            }
            if (BaseProfile != null)
            {
                write($"{indent}{nameof(BaseProfile)}: \"{BaseProfile}\"");
            }
            if (ContentScriptType != null)
            {
                write($"{indent}{nameof(ContentScriptType)}: \"{ContentScriptType}\"");
            }
            if (ContentStyleType != null)
            {
                write($"{indent}{nameof(ContentStyleType)}: \"{ContentStyleType}\"");
            }
        }
    }
}
