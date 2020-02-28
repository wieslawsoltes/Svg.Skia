using System;
using Xml;

namespace Svg
{
    [Element("svg")]
    public class SvgFragment : SvgElement,
                               ISvgPresentationAttributes,
                               ISvgTestsAttributes,
                               ISvgStylableAttributes,
                               ISvgResourcesAttributes
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => GetAttribute("width");
            set => SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => GetAttribute("height");
            set => SetAttribute("height", value);
        }

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

        [Attribute("version", SvgNamespace)]
        public string? Version
        {
            get => GetAttribute("version");
            set => SetAttribute("version", value);
        }

        [Attribute("baseProfile", SvgNamespace)]
        public string? BaseProfile
        {
            get => GetAttribute("baseProfile");
            set => SetAttribute("baseProfile", value);
        }

        [Attribute("contentScriptType", SvgNamespace)]
        public string? ContentScriptType
        {
            get => GetAttribute("contentScriptType");
            set => SetAttribute("contentScriptType", value);
        }

        [Attribute("contentStyleType", SvgNamespace)]
        public string? ContentStyleType
        {
            get => GetAttribute("contentStyleType");
            set => SetAttribute("contentStyleType", value);
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
