using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feMergeNode")]
    public class SvgMergeNode : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("in", SvgNamespace)]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Input != null)
            {
                write($"{indent}{nameof(Input)}: \"{Input}\"");
            }
        }
    }
}
