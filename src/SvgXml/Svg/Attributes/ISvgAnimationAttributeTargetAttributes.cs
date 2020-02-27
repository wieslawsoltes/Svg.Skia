using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationAttributeTargetAttributes : IElement
    {
        [Attribute("attributeType", SvgAttributes.SvgNamespace)]
        public string? AttributeType
        {
            get => GetAttribute("attributeType");
            set => SetAttribute("attributeType", value);
        }

        [Attribute("attributeName", SvgAttributes.SvgNamespace)]
        public string? AttributeName
        {
            get => GetAttribute("attributeName");
            set => SetAttribute("attributeName", value);
        }
    }
}
