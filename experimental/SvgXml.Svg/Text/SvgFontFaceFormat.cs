using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
{
    [Element("font-face-format")]
    public class SvgFontFaceFormat : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("string", SvgNamespace)]
        public string? String
        {
            get => this.GetAttribute("string", false, null); // TODO:
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
    }
}
