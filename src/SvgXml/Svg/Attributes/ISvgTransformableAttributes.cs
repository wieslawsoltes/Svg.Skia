using System;
using Xml;

namespace Svg
{
    public interface ISvgTransformableAttributes : IElement
    {
        [Attribute("transform", SvgElement.SvgNamespace)]
        public string? Transform
        {
            get => GetAttribute("transform");
            set => SetAttribute("transform", value);
        }
    }
}
