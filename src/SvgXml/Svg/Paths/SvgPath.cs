using System;
using Xml;

namespace Svg
{
    [Element("path")]
    public class SvgPath : SvgMarkerElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        [Attribute("d")]
        public string? PathData
        {
            get => GetAttribute("d");
            set => SetAttribute("d", value);
        }

        [Attribute("pathLength")]
        public string? PathLength
        {
            get => GetAttribute("pathLength");
            set => SetAttribute("pathLength", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (PathData != null)
            {
                Console.WriteLine($"{indent}{nameof(PathData)}='{PathData}'");
            }
            if (PathLength != null)
            {
                Console.WriteLine($"{indent}{nameof(PathLength)}='{PathLength}'");
            }
        }
    }
}
