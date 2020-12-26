using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.ClippingAndMasking
{
    [Element("clipPath")]
    public class SvgClipPath : SvgStylableElement,
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

        // SvgClipPath

        [Attribute("clipPathUnits", SvgNamespace)]
        public string? ClipPathUnits
        {
            get => this.GetAttribute("clipPathUnits", false, "userSpaceOnUse");
            set => this.SetAttribute("clipPathUnits", value);
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
                // SvgClipPath
                case "clipPathUnits":
                    ClipPathUnits = value;
                    break;
            }
        }
    }
}
