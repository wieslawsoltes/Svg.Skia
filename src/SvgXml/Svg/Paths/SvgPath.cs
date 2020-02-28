using System;
using Xml;

namespace Svg
{
    [Element("path")]
    public class SvgPath : SvgMarkerElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes,
        ISvgTransformableAttributes
    {
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
