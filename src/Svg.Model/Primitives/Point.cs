using System;

namespace Svg.Model.Primitives
{
    public readonly struct Point
    {
        public float X { get; }
        public float Y { get; }

        public static readonly Point Empty = default;

        public readonly bool IsEmpty => X == default && Y == default;

        public Point(float x, float y)
        {
            X = x;
            Y = y;
        }
        
        public override string ToString()
        {
            return FormattableString.Invariant($"{X}, {Y}");
        }
    }
}
