using System;
using Xml;

namespace Svg
{
    public abstract class SvgTextContent : SvgVisualElement
    {
        [Attribute("lengthAdjust", SvgNamespace)]
        public string? LengthAdjust
        {
            get => this.GetAttribute("lengthAdjust");
            set => this.SetAttribute("lengthAdjust", value);
        }

        [Attribute("textLength", SvgNamespace)]
        public string? TextLength
        {
            get => this.GetAttribute("textLength");
            set => this.SetAttribute("textLength", value);
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
