using System;

namespace ShimSkiaSharp.Primitives
{
    public readonly struct SKPoint
    {
        public float X { get; }

        public float Y { get; }

        public static readonly SKPoint Empty = default;

        public readonly bool IsEmpty => X == default && Y == default;

        public SKPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() 
            => FormattableString.Invariant($"{X}, {Y}");
    }
}
