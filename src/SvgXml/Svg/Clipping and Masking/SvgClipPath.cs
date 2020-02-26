using System;
using Xml;

namespace Svg
{
    [Element("clipPath")]
    public class SvgClipPath : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        [Attribute("clipPathUnits")]
        public string? ClipPathUnits
        {
            get => GetAttribute("clipPathUnits");
            set => SetAttribute("clipPathUnits", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (ClipPathUnits != null)
            {
                Console.WriteLine($"{indent}{nameof(ClipPathUnits)}: \"{ClipPathUnits}\"");
            }
        }
    }
}
