using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feDisplacementMap")]
    public class SvgDisplacementMap : SvgFilterPrimitive,
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

        [Attribute("in2", SvgNamespace)]
        public string? Input2
        {
            get => this.GetAttribute("in2");
            set => this.SetAttribute("in2", value);
        }

        [Attribute("scale", SvgNamespace)]
        public string? Scale
        {
            get => this.GetAttribute("scale");
            set => this.SetAttribute("scale", value);
        }

        [Attribute("xChannelSelector", SvgNamespace)]
        public string? XChannelSelector
        {
            get => this.GetAttribute("xChannelSelector");
            set => this.SetAttribute("xChannelSelector", value);
        }

        [Attribute("yChannelSelector", SvgNamespace)]
        public string? YChannelSelector
        {
            get => this.GetAttribute("yChannelSelector");
            set => this.SetAttribute("yChannelSelector", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Input != null)
            {
                write($"{indent}{nameof(Input)}: \"{Input}\"");
            }
            if (Input2 != null)
            {
                write($"{indent}{nameof(Input2)}: \"{Input2}\"");
            }
            if (Scale != null)
            {
                write($"{indent}{nameof(Scale)}: \"{Scale}\"");
            }
            if (XChannelSelector != null)
            {
                write($"{indent}{nameof(XChannelSelector)}: \"{XChannelSelector}\"");
            }
            if (YChannelSelector != null)
            {
                write($"{indent}{nameof(YChannelSelector)}: \"{YChannelSelector}\"");
            }
        }
    }
}
