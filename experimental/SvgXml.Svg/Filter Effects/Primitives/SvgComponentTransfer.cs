using SvgXml.Xml.Attributes;

namespace SvgXml.Svg.FilterEffects
{
    public abstract class SvgComponentTransferFunction : SvgElement
    {
        [Attribute("type", SvgNamespace)]
        public override string? Type
        {
            get => this.GetAttribute("type", false, null); // TODO:
            set => this.SetAttribute("type", value);
        }

        [Attribute("tableValues", SvgNamespace)]
        public string? TableValues
        {
            get => this.GetAttribute("tableValues", false, "");
            set => this.SetAttribute("tableValues", value);
        }

        [Attribute("slope", SvgNamespace)]
        public string? Slope
        {
            get => this.GetAttribute("slope", false, "1");
            set => this.SetAttribute("slope", value);
        }

        [Attribute("intercept", SvgNamespace)]
        public string? Intercept
        {
            get => this.GetAttribute("intercept", false, "0");
            set => this.SetAttribute("intercept", value);
        }

        [Attribute("amplitude", SvgNamespace)]
        public string? Amplitude
        {
            get => this.GetAttribute("amplitude", false, "1");
            set => this.SetAttribute("amplitude", value);
        }

        [Attribute("exponent", SvgNamespace)]
        public string? Exponent
        {
            get => this.GetAttribute("exponent", false, "1");
            set => this.SetAttribute("exponent", value);
        }

        [Attribute("offset", SvgNamespace)]
        public string? Offset
        {
            get => this.GetAttribute("offset", false, "0");
            set => this.SetAttribute("offset", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "type":
                    Type = value;
                    break;
                case "tableValues":
                    TableValues = value;
                    break;
                case "slope":
                    Slope = value;
                    break;
                case "intercept":
                    Intercept = value;
                    break;
                case "amplitude":
                    Amplitude = value;
                    break;
                case "exponent":
                    Exponent = value;
                    break;
                case "offset":
                    Offset = value;
                    break;
            }
        }
    }

    [Element("feComponentTransfer")]
    public class SvgComponentTransfer : SvgFilterPrimitive,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
        [Attribute("in", SvgNamespace)]
        public string? Input
        {
            get => this.GetAttribute("in", false, null);
            set => this.SetAttribute("in", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "in":
                    Input = value;
                    break;
            }
        }
    }
}
