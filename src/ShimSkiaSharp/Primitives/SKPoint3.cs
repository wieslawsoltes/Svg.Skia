using System;

namespace ShimSkiaSharp.Primitives
{
    public readonly struct SKPoint3
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static readonly SKPoint3 Empty;

        public readonly bool IsEmpty => X == default && Y == default && Z == default;

        public SKPoint3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return FormattableString.Invariant($"{X}, {Y}, {Z}");
        }
    }
}
