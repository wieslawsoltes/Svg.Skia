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
