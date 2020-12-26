using System;
using Xml;

namespace Svg
{
    [Element("style")]
    public class SvgStyle : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("type", SvgNamespace)]
        public override string? Type
        {
            // TODO: https://www.w3.org/TR/SVG11/styling.html#StyleElement
            get => this.GetAttribute("type", false, null);
            set => this.SetAttribute("type", value);
        }

        [Attribute("media", SvgNamespace)]
        public string? Media
        {
            get => this.GetAttribute("media", false, "all");
            set => this.SetAttribute("media", value);
        }

        [Attribute("title", SvgNamespace)]
        public override string? Title
        {
            get => this.GetAttribute("title", false, null);
            set => this.SetAttribute("title", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "type":
                    Type = value;
                    break;
                case "media":
                    Media = value;
                    break;
                case "title":
                    Title = value;
                    break;
            }
        }
    }
}
