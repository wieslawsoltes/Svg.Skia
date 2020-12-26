using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Text
{
    public abstract class SvgTextContent : SvgStylableElement
    {
        [Attribute("lengthAdjust", SvgNamespace)]
        public string? LengthAdjust
        {
            get => this.GetAttribute("lengthAdjust", false, "spacing");
            set => this.SetAttribute("lengthAdjust", value);
        }

        [Attribute("textLength", SvgNamespace)]
        public string? TextLength
        {
            get => this.GetAttribute("textLength", false, null);
            set => this.SetAttribute("textLength", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "lengthAdjust":
                    LengthAdjust = value;
                    break;
                case "textLength":
                    TextLength = value;
                    break;
            }
        }
    }
}
