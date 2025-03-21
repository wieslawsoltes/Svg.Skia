using System;

namespace ShimSkiaSharp;

public readonly struct SKSize(float width, float height)
{
    public float Width { get; } = width;

    public float Height { get; } = height;

    public static readonly SKSize Empty;

    public readonly bool IsEmpty => Width == default && Height == default;

    public override string ToString() 
        => FormattableString.Invariant($"{Width}, {Height}");
}