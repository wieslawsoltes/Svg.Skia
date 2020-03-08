using System;
using Xml;

namespace Svg
{
    [Element("path")]
    public class SvgPath : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
        // ISvgTransformableAttributes

        [Attribute("transform", SvgNamespace)]
        public string? Transform
        {
            get => this.GetAttribute("transform", false, null);
            set => this.SetAttribute("transform", value);
        }

        // SvgPath

        [Attribute("d", SvgNamespace)]
        public string? PathData
        {
            get => this.GetAttribute("d");
            set => this.SetAttribute("d", value);
        }

        [Attribute("pathLength", SvgNamespace)]
        public string? PathLength
        {
            get => this.GetAttribute("pathLength");
            set => this.SetAttribute("pathLength", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                // ISvgTransformableAttributes
                case "transform":
                    Transform = value;
                    break;
                // SvgPath
                case "d":
                    PathData = value;
                    break;
                case "pathLength":
                    PathLength = value;
                    break;
            }
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (PathData != null)
            {
                write($"{indent}{nameof(PathData)}: \"{PathData}\"");
            }
            if (PathLength != null)
            {
                write($"{indent}{nameof(PathLength)}: \"{PathLength}\"");
            }
        }
    }
}
