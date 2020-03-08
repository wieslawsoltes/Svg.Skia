using System;
using Xml;

namespace Svg
{
    [Element("font-face-format")]
    public class SvgFontFaceFormat : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("string", SvgNamespace)]
        public string? String
        {
            get => this.GetAttribute("string");
            set => this.SetAttribute("string", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "string":
                    String = value;
                    break;
            }
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (String != null)
            {
                write($"{indent}{nameof(String)}: \"{String}\"");
            }
        }
    }
}
