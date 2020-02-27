using System;
using Xml;

namespace Svg
{
    public abstract class SvgTextContent : SvgVisualElement
    {
        [Attribute("lengthAdjust", SvgAttributes.SvgNamespace)]
        public string? LengthAdjust
        {
            get => GetAttribute("lengthAdjust");
            set => SetAttribute("lengthAdjust", value);
        }

        [Attribute("textLength", SvgAttributes.SvgNamespace)]
        public string? TextLength
        {
            get => GetAttribute("textLength");
            set => SetAttribute("textLength", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (LengthAdjust != null)
            {
                write($"{indent}{nameof(LengthAdjust)}: \"{LengthAdjust}\"");
            }
            if (TextLength != null)
            {
                write($"{indent}{nameof(TextLength)}: \"{TextLength}\"");
            }
        }
    }
}
