using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationAttributeTargetAttributes : IElement
    {
        [Attribute("attributeType", SvgElement.SvgNamespace)]
        public string? AttributeType
        {
            get => this.GetAttribute("attributeType");
            set => this.SetAttribute("attributeType", value);
        }

        [Attribute("attributeName", SvgElement.SvgNamespace)]
        public string? AttributeName
        {
            get => this.GetAttribute("attributeName");
            set => this.SetAttribute("attributeName", value);
        }
    }
}
