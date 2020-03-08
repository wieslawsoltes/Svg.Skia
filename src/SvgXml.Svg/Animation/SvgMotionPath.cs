using System;
using Xml;

namespace Svg
{
    [Element("mpath")]
    public class SvgMotionPath : SvgElement,
        ISvgCommonAttributes,
        ISvgResourcesAttributes
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
