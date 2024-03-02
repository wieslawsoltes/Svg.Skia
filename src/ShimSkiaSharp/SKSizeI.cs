using System;

namespace ShimSkiaSharp;

public readonly struct SKSizeI(int width, int height)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    public static readonly SKSizeI Empty;

    public readonly bool IsEmpty => Width == default && Height == default;

    public override string ToString() 
        => FormattableString.Invariant($"{Width}, {Height}");
}