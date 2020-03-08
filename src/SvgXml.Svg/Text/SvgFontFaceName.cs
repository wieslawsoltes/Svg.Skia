using System;
using Xml;

namespace Svg
{
    [Element("font-face-name")]
    public class SvgFontFaceName : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("name", SvgNamespace)]
        public string? Name
        {
            get => this.GetAttribute("name");
            set => this.SetAttribute("name", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "name":
                    Name = value;
                    break;
            }
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Name != null)
            {
                write($"{indent}{nameof(Name)}: \"{Name}\"");
            }
        }
    }
}
