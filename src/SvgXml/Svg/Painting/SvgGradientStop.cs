using System;
using Xml;

namespace Svg
{
    [Element("stop")]
    public class SvgGradientStop : SvgElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
        [Attribute("offset", SvgNamespace)]
        public string? Offset
        {
            get => this.GetAttribute("offset", false, null);
            set => this.SetAttribute("offset", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Offset != null)
            {
                write($"{indent}{nameof(Offset)}: \"{Offset}\"");
            }
        }
    }
}
