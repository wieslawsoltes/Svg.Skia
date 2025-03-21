using System;

namespace ShimSkiaSharp;

public readonly struct SKPoint(float x, float y)
{
    public float X { get; } = x;

    public float Y { get; } = y;

    public static readonly SKPoint Empty = default;

    public readonly bool IsEmpty => X == default && Y == default;

    public override string ToString() 
        => FormattableString.Invariant($"{X}, {Y}");
}