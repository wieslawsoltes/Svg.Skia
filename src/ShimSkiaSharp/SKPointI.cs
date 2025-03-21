using System;

namespace ShimSkiaSharp;

public readonly struct SKPointI(int x, int y)
{
    public int X { get; } = x;

    public int Y { get; } = y;

    public static readonly SKPointI Empty;

    public readonly bool IsEmpty => X == default && Y == default;

    public override string ToString() 
        => FormattableString.Invariant($"{X}, {Y}");
}