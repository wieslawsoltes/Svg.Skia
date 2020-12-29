using System;

namespace Svg.Model.Primitives
{
    public readonly struct Size
    {
        public float Width { get; }
        public float Height { get; }

        public static readonly Size Empty;

        public readonly bool IsEmpty => Width == default && Height == default;

        public Size(float width, float height)
        {
            Width = width;
            Height = height;
        }
        
        public override string ToString()
        {
            return FormattableString.Invariant($"{Width}, {Height}");
        }
    }
}
