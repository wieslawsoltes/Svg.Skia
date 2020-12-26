using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects
{
    public abstract class SvgFilterPrimitive : SvgStylableElement
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x", false, "0%");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y", false, "0%");
            set => this.SetAttribute("y", value);
        }

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => this.GetAttribute("width", false, "100%");
            set => this.SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => this.GetAttribute("height", false, "100%");
            set => this.SetAttribute("height", value);
        }

        [Attribute("result", SvgNamespace)]
        public string? Result
        {
            get => this.GetAttribute("result", false, null);
            set => this.SetAttribute("result", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "x":
                    X = value;
                    break;
                case "y":
                    Y = value;
                    break;
                case "width":
                    Width = value;
                    break;
                case "height":
                    Height = value;
                    break;
                case "result":
                    Result = value;
                    break;
            }
        }
    }
}
