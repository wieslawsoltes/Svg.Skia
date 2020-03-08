using System;
using Xml;

namespace Svg
{
    public abstract class SvgTextContent : SvgStylableElement
    {
        [Attribute("lengthAdjust", SvgNamespace)]
        public string? LengthAdjust
        {
            get => this.GetAttribute("lengthAdjust", false, "spacing");
            set => this.SetAttribute("lengthAdjust", value);
        }

        [Attribute("textLength", SvgNamespace)]
        public string? TextLength
        {
            get => this.GetAttribute("textLength", false, null);
            set => this.SetAttribute("textLength", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                case "lengthAdjust":
                    LengthAdjust = value;
                    break;
                case "textLength":
                    TextLength = value;
                    break;
            }
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
