using SvgXml.Svg.Attributes;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Scripting
{
    [Element("script")]
    public class SvgScript : SvgElement,
        ISvgCommonAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("type", SvgNamespace)]
        public override string? Type
        {
            get => this.GetAttribute("type", false, null); // TODO: inherit from svg contentScriptType’
            set => this.SetAttribute("type", value);
        }

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
                case "type":
                    Type = value;
                    break;
                case "href":
                    Href = value;
                    break;
            }
        }
    }
}
