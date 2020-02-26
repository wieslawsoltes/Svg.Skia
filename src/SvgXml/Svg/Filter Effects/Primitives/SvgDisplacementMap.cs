using System;
using Xml;

namespace Svg.FilterEffects
{
    [Element("feDisplacementMap")]
    public class SvgDisplacementMap : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        [Attribute("in")]
        public string? Input
        {
            get => GetAttribute("in");
            set => SetAttribute("in", value);
        }

        [Attribute("in2")]
        public string? Input2
        {
            get => GetAttribute("in2");
            set => SetAttribute("in2", value);
        }

        [Attribute("scale")]
        public string? Scale
        {
            get => GetAttribute("scale");
            set => SetAttribute("scale", value);
        }

        [Attribute("xChannelSelector")]
        public string? XChannelSelector
        {
            get => GetAttribute("xChannelSelector");
            set => SetAttribute("xChannelSelector", value);
        }

        [Attribute("yChannelSelector")]
        public string? YChannelSelector
        {
            get => GetAttribute("yChannelSelector");
            set => SetAttribute("yChannelSelector", value);
        }

        public override void Print(string indent)
        {
            base.Print(indent);

            if (Input != null)
            {
                Console.WriteLine($"{indent}{nameof(Input)}='{Input}'");
            }
            if (Input2 != null)
            {
                Console.WriteLine($"{indent}{nameof(Input2)}='{Input2}'");
            }
            if (Scale != null)
            {
                Console.WriteLine($"{indent}{nameof(Scale)}='{Scale}'");
            }
            if (XChannelSelector != null)
            {
                Console.WriteLine($"{indent}{nameof(XChannelSelector)}='{XChannelSelector}'");
            }
            if (YChannelSelector != null)
            {
                Console.WriteLine($"{indent}{nameof(YChannelSelector)}='{YChannelSelector}'");
            }
        }
    }
}
