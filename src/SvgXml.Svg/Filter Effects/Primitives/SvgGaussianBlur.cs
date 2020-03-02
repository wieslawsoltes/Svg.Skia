using System;
using Xml;


namespace Svg.FilterEffects
{
    [Element("feGaussianBlur")]
    public class SvgGaussianBlur : SvgFilterPrimitive,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
        [Attribute("in", SvgNamespace)]
        public string? Input
        {
            get => this.GetAttribute("in");
            set => this.SetAttribute("in", value);
        }

        [Attribute("stdDeviation", SvgNamespace)]
        public string? StdDeviation
        {
            get => this.GetAttribute("stdDeviation");
            set => this.SetAttribute("stdDeviation", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Input != null)
            {
                write($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (StdDeviation != null)
            {
                write($"{indent}{nameof(StdDeviation)}: \"{StdDeviation}\"");
            }
        }
    }
}
