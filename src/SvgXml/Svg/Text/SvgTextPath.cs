using System;
using Xml;

namespace Svg
{
    [Element("textPath")]
    public class SvgTextPath : SvgTextContent, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgResourcesAttributes
    {
        [Attribute("href", SvgElement.XLinkNamespace)]
        public string? Href
        {
            get => GetAttribute("href");
            set => SetAttribute("href", value);
        }

        [Attribute("startOffset", SvgElement.SvgNamespace)]
        public string? StartOffset
        {
            get => GetAttribute("startOffset");
            set => SetAttribute("startOffset", value);
        }

        [Attribute("method", SvgElement.SvgNamespace)]
        public string? Method
        {
            get => GetAttribute("method");
            set => SetAttribute("method", value);
        }

        [Attribute("spacing", SvgElement.SvgNamespace)]
        public string? Spacing
        {
            get => GetAttribute("spacing");
            set => SetAttribute("spacing", value);
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
