using System;
using Xml;

namespace Svg
{
    [Element("font-face-format")]
    public class SvgFontFaceFormat : SvgElement
    {
        [Attribute("string", SvgNamespace)]
        public string? String
        {
            get => GetAttribute("string");
            set => SetAttribute("string", value);
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
