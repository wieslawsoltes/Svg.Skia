using System;
using Xml;

namespace Svg.FilterEffects
{
    public abstract class SvgFilterPrimitive : SvgElement
    {
        [Attribute("x", SvgNamespace)]
        public string? X
        {
            get => this.GetAttribute("x");
            set => this.SetAttribute("x", value);
        }

        [Attribute("y", SvgNamespace)]
        public string? Y
        {
            get => this.GetAttribute("y");
            set => this.SetAttribute("y", value);
        }

        [Attribute("width", SvgNamespace)]
        public string? Width
        {
            get => this.GetAttribute("width");
            set => this.SetAttribute("width", value);
        }

        [Attribute("height", SvgNamespace)]
        public string? Height
        {
            get => this.GetAttribute("height");
            set => this.SetAttribute("height", value);
        }

        [Attribute("result", SvgNamespace)]
        public string? Result
        {
            get => this.GetAttribute("result");
            set => this.SetAttribute("result", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (X != null)
            {
                write($"{indent}{nameof(X)}: \"{X}\"");
            }
            if (Y != null)
            {
                write($"{indent}{nameof(Y)}: \"{Y}\"");
            }
            if (Width != null)
            {
                write($"{indent}{nameof(Width)}: \"{Width}\"");
            }
            if (Height != null)
            {
                write($"{indent}{nameof(Height)}: \"{Height}\"");
            }
            if (Result != null)
            {
                write($"{indent}{nameof(Result)}: \"{Result}\"");
            }
        }
    }
}
