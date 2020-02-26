using System;
using Xml;

namespace Svg
{
    [Element("svg")]
    public class SvgFragment : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        [Attribute("x")]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y")]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("width")]
        public string? Width
        {
            get => GetAttribute("width");
            set => SetAttribute("width", value);
        }

        [Attribute("height")]
        public string? Height
        {
            get => GetAttribute("height");
            set => SetAttribute("height", value);
        }

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

        [Attribute("zoomAndPan")]
        public string? ZoomAndPan
        {
            get => GetAttribute("zoomAndPan");
            set => SetAttribute("zoomAndPan", value);
        }

        [Attribute("version")]
        public string? Version
        {
            get => GetAttribute("version");
            set => SetAttribute("version", value);
        }

        [Attribute("baseProfile")]
        public string? BaseProfile
        {
            get => GetAttribute("baseProfile");
            set => SetAttribute("baseProfile", value);
        }

        [Attribute("contentScriptType")]
        public string? ContentScriptType
        {
            get => GetAttribute("contentScriptType");
            set => SetAttribute("contentScriptType", value);
        }

        [Attribute("contentStyleType")]
        public string? ContentStyleType
        {
            get => GetAttribute("contentStyleType");
            set => SetAttribute("contentStyleType", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (X != null)
            {
                Console.WriteLine($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                Console.WriteLine($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Width != null)
            {
                Console.WriteLine($"{indent}{nameof(Width)}: \"{Width}\"");
            }
            if (Height != null)
            {
                Console.WriteLine($"{indent}{nameof(Height)}: \"{Height}\"");
            }
            if (ViewBox != null)
            {
                Console.WriteLine($"{indent}{nameof(ViewBox)}: \"{ViewBox}\"");
            }
            if (AspectRatio != null)
            {
                Console.WriteLine($"{indent}{nameof(AspectRatio)}: \"{AspectRatio}\"");
            }
            if (ZoomAndPan != null)
            {
                Console.WriteLine($"{indent}{nameof(ZoomAndPan)}: \"{ZoomAndPan}\"");
            }
            if (Version != null)
            {
                Console.WriteLine($"{indent}{nameof(Version)}: \"{Version}\"");
            }
            if (BaseProfile != null)
            {
                Console.WriteLine($"{indent}{nameof(BaseProfile)}: \"{BaseProfile}\"");
            }
            if (ContentScriptType != null)
            {
                Console.WriteLine($"{indent}{nameof(ContentScriptType)}: \"{ContentScriptType}\"");
            }
            if (ContentStyleType != null)
            {
                Console.WriteLine($"{indent}{nameof(ContentStyleType)}: \"{ContentStyleType}\"");
            }
        }
    }
}
