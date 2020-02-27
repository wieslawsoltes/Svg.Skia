using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feDisplacementMap")]
    public class SvgDisplacementMap : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in", SvgElement.SvgNamespace)]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("in2", SvgElement.SvgNamespace)]
        public string? Input2
        {
            get => GetAttribute("in2");
            set => SetAttribute("in2", value);
        }

        [Attribute("scale", SvgElement.SvgNamespace)]
        public string? Scale
        {
            get => GetAttribute("scale");
            set => SetAttribute("scale", value);
        }

        [Attribute("xChannelSelector", SvgElement.SvgNamespace)]
        public string? XChannelSelector
        {
            get => GetAttribute("xChannelSelector");
            set => SetAttribute("xChannelSelector", value);
        }

        [Attribute("yChannelSelector", SvgElement.SvgNamespace)]
        public string? YChannelSelector
        {
            get => GetAttribute("yChannelSelector");
            set => SetAttribute("yChannelSelector", value);
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
