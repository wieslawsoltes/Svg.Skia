using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.Text
{
    public abstract class SvgTextPositioning : SvgTextContent
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x", false, "0");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y", false, "0");
            set => this.SetAttribute("y", value);
        }

        [Attribute("dx", SvgNamespace)]
        public string? Dx
        {
            get => this.GetAttribute("dx", false, null); // TODO:
            set => this.SetAttribute("dx", value);
        }

        [Attribute("dy", SvgNamespace)]
        public string? Dy
        {
            get => this.GetAttribute("dy", false, null); // TODO:
            set => this.SetAttribute("dy", value);
        }

        [Attribute("rotate", SvgNamespace)]
        public string? Rotate
        {
            get => this.GetAttribute("rotate", false, null); // TODO:
            set => this.SetAttribute("rotate", value);
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
                case "dx":
                    Dx = value;
                    break;
                case "dy":
                    Dy = value;
                    break;
                case "rotate":
                    Rotate = value;
                    break;
            }
        }
    }
}
