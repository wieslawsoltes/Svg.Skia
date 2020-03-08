using System;
using Xml;

namespace Svg
{
    [Element("font-face-uri")]
    public class SvgFontFaceUri : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("href", XLinkNamespace)]
        public override string? Href
        {
            get => this.GetAttribute("href");
            set => this.SetAttribute("href", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "href":
                    Href = value;
                    break;
            }
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
