using System;
using Xml;

namespace Svg
{
    public interface ISvgTransformableAttributes : IElement
    {
        [Attribute("transform", SvgElement.SvgNamespace)]
        public string? Transform
        {
            get => this.GetAttribute("transform", false, null);
            set => this.SetAttribute("transform", value);
        }
    }
}
