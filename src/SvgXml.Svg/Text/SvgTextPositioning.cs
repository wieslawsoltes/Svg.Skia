using System;
using Xml;

namespace Svg
{
    public abstract class SvgTextPositioning : SvgTextContent
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

        [Attribute("dx", SvgNamespace)]
        public string? Dx
        {
            get => this.GetAttribute("dx");
            set => this.SetAttribute("dx", value);
        }

        [Attribute("dy", SvgNamespace)]
        public string? Dy
        {
            get => this.GetAttribute("dy");
            set => this.SetAttribute("dy", value);
        }

        [Attribute("rotate", SvgNamespace)]
        public string? Rotate
        {
            get => this.GetAttribute("rotate");
            set => this.SetAttribute("rotate", value);
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
            if (Dx != null)
            {
                write($"{indent}{nameof(Dx)}: \"{Dx}\"");
            }
            if (Dy != null)
            {
                write($"{indent}{nameof(Dy)}: \"{Dy}\"");
            }
            if (Rotate != null)
            {
                write($"{indent}{nameof(Rotate)}: \"{Rotate}\"");
            }
        }
    }
}
