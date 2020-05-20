using System;
using Xml;

namespace Svg
{
    [Element("svg")]
    public class SvgFragment : SvgStylableElement,
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

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "x":
                    X = value;
                    break;
                case "y":
                    Y = value;
                    break;
                case "width":
                    Width = value;
                    break;
                case "height":
                    Height = value;
                    break;
                case "viewBox":
                    ViewBox = value;
                    break;
                case "preserveAspectRatio":
                    AspectRatio = value;
                    break;
                case "zoomAndPan":
                    ZoomAndPan = value;
                    break;
                case "version":
                    Version = value;
                    break;
                case "baseProfile":
                    BaseProfile = value;
                    break;
                case "contentScriptType":
                    ContentScriptType = value;
                    break;
                case "contentStyleType":
                    ContentStyleType = value;
                    break;
            }
        }
    }
}
