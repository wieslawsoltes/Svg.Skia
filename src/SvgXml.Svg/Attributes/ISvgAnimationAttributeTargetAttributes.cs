using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationAttributeTargetAttributes : IElement
    {
        string? AttributeType { get; set; }
        string? AttributeName { get; set; }
    }
}
