using System;
using Xml;

namespace Svg
{
    public interface ISvgTransformableAttributes : IElement, ISvgAttributePrinter
    {
        [Attribute("transform")]
        public string? Transform
        {
            get => GetAttribute("transform");
            set => SetAttribute("transform", value);
        }

        public void PrintTransformableAttributes(string indent)
        {
            if (Transform != null)
            {
                Console.WriteLine($"{indent}{nameof(Transform)}='{Transform}'");
            }
        }
    }
}
