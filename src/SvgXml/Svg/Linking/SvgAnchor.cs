using System;
using Xml;

namespace Svg
{
    [Element("a")]
    public class SvgAnchor : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes, ISvgTransformableAttributes
    {
        [Attribute("target", SvgAttributes.SvgNamespace)]
        public string? Target
        {
            get => GetAttribute("target");
            set => SetAttribute("target", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Target != null)
            {
                write($"{indent}{nameof(Target)}: \"{Target}\"");
            }
        }
    }
}
