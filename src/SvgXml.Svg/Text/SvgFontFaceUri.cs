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
            get => this.GetAttribute("href", false, null);
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
    }
}
