using System;

namespace Svg.Model.Primitives
{
    public readonly struct SizeI
    {
        public int Width { get; }
        public int Height { get; }

        public static readonly SizeI Empty;

        public readonly bool IsEmpty => Width == default && Height == default;

        public SizeI(int width, int height)
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
