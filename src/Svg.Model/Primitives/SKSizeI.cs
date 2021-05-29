using System;

namespace Svg.Model.Primitives
{
    public readonly struct SKSizeI
    {
        public int Width { get; }
        public int Height { get; }

        public static readonly SKSizeI Empty;

        public readonly bool IsEmpty => Width == default && Height == default;

        public SKSizeI(int width, int height)
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
