using System;
using Xml;

namespace Svg
{
    public abstract class SvgTextPositioning : SvgTextContent
    {
        [Attribute("x", SvgElement.SvgNamespace)]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y", SvgElement.SvgNamespace)]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("dx", SvgElement.SvgNamespace)]
        public string? Dx
        {
            get => GetAttribute("dx");
            set => SetAttribute("dx", value);
        }

        [Attribute("dy", SvgElement.SvgNamespace)]
        public string? Dy
        {
            get => GetAttribute("dy");
            set => SetAttribute("dy", value);
        }

        [Attribute("rotate", SvgElement.SvgNamespace)]
        public string? Rotate
        {
            get => GetAttribute("rotate");
            set => SetAttribute("rotate", value);
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
