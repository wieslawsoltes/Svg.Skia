using System;
using Xml;

namespace Svg
{
    [Element("textPath")]
    public class SvgTextPath : SvgTextContent,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgTestsAttributes,
        ISvgStylableAttributes,
        ISvgResourcesAttributes
    {
        [Attribute("href", XLinkNamespace)]
        public string? Href
        {
            get => this.GetAttribute("href");
            set => this.SetAttribute("href", value);
        }

        [Attribute("startOffset", SvgNamespace)]
        public string? StartOffset
        {
            get => this.GetAttribute("startOffset");
            set => this.SetAttribute("startOffset", value);
        }

        [Attribute("method", SvgNamespace)]
        public string? Method
        {
            get => this.GetAttribute("method");
            set => this.SetAttribute("method", value);
        }

        [Attribute("spacing", SvgNamespace)]
        public string? Spacing
        {
            get => this.GetAttribute("spacing");
            set => this.SetAttribute("spacing", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (StartOffset != null)
            {
                write($"{indent}{nameof(StartOffset)}: \"{StartOffset}\"");
            }
            if (Method != null)
            {
                write($"{indent}{nameof(Method)}: \"{Method}\"");
            }
            if (Spacing != null)
            {
                write($"{indent}{nameof(Spacing)}: \"{Spacing}\"");
            }
            if (Href != null)
            {
                write($"{indent}{nameof(Href)}: \"{Href}\"");
            }
        }
    }
}
