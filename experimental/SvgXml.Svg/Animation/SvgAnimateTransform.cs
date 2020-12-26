using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
{
    [Element("animateTransform")]
    public class SvgAnimateTransform : SvgAnimationElement,
        ISvgCommonAttributes,
        ISvgTestsAttributes,
        ISvgResourcesAttributes,
        ISvgXLinkAttributes,
        ISvgAnimationEventAttributes,
        ISvgAnimationAttributeTargetAttributes,
        ISvgAnimationTimingAttributes,
        ISvgAnimationValueAttributes,
        ISvgAnimationAdditionAttributes
    {
        [Attribute("type", SvgNamespace)]
        public override string? Type
        {
            get => this.GetAttribute("type", false, "translate");
            set => this.SetAttribute("type", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "type":
                    Type = value;
                    break;
            }
        }
    }
}
