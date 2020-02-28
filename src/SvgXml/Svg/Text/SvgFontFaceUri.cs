using System;
using Xml;

namespace Svg
{
    [Element("font-face-uri")]
    public class SvgFontFaceUri : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("href", XLinkNamespace)]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
