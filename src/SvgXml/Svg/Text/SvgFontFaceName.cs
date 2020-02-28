using System;
using Xml;

namespace Svg
{
    [Element("font-face-name")]
    public class SvgFontFaceName : SvgElement
    {
        [Attribute("name", SvgNamespace)]
        public string? Name
        {
            get => GetAttribute("name");
            set => SetAttribute("name", value);
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
