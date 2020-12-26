using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Paths
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
            get => this.GetAttribute("d", false, null);
            set => this.SetAttribute("d", value);
        }

        [Attribute("pathLength", SvgNamespace)]
        public string? PathLength
        {
            get => this.GetAttribute("pathLength", false, null);
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
    }
}
