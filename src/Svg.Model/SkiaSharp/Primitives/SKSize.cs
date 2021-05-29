using System;

namespace Svg.Model.Primitives
{
    public readonly struct SKSize
    {
        public float Width { get; }
        public float Height { get; }

        public static readonly SKSize Empty;

        public readonly bool IsEmpty => Width == default && Height == default;

        public SKSize(float width, float height)
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
