using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Animation
{
    [Element("set")]
    public class SvgSet : SvgAnimationElement,
        ISvgCommonAttributes,
        ISvgTestsAttributes,
        ISvgResourcesAttributes,
        ISvgXLinkAttributes,
        ISvgAnimationEventAttributes,
        ISvgAnimationAttributeTargetAttributes,
        ISvgAnimationTimingAttributes
    {
        [Attribute("to", SvgNamespace)]
        public override string? To
        {
            get => this.GetAttribute("to", false, null);
            set => this.SetAttribute("to", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "to":
                    To = value;
                    break;
            }
        }
    }
}
