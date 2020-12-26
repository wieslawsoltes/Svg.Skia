using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
{
    [Element("symbol")]
    public class SvgSymbol : SvgStylableElement,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("viewBox", SvgNamespace)]
        public string? ViewBox
        {
            get => this.GetAttribute("viewBox", false, null);
            set => this.SetAttribute("viewBox", value);
        }

        [Attribute("preserveAspectRatio", SvgNamespace)]
        public string? AspectRatio
        {
            get => this.GetAttribute("preserveAspectRatio", false, "xMidYMid meet");
            set => this.SetAttribute("preserveAspectRatio", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "viewBox":
                    ViewBox = value;
                    break;
                case "preserveAspectRatio":
                    AspectRatio = value;
                    break;
            }
        }
    }
}
