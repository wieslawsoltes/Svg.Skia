using System;

namespace ShimSkiaSharp;

public readonly struct SKPoint3(float x, float y, float z)
{
    public float X { get; } = x;

    public float Y { get; } = y;

    public float Z { get; } = z;

    public static readonly SKPoint3 Empty;

    public readonly bool IsEmpty => X == default && Y == default && Z == default;

    public override string ToString() 
        => FormattableString.Invariant($"{X}, {Y}, {Z}");
}