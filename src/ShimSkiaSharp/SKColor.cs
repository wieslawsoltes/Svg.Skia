using System;

namespace ShimSkiaSharp;

public readonly struct SKColor(byte red, byte green, byte blue, byte alpha)
{
    public byte Red { get; } = red;

    public byte Green { get; } = green;

    public byte Blue { get; } = blue;

    public byte Alpha { get; } = alpha;

    public static readonly SKColor Empty = default;

    public static implicit operator SKColorF(SKColor color)
    {
        return new(
            color.Red * (1 / 255.0f),
            color.Green * (1 / 255.0f),
            color.Blue * (1 / 255.0f),
            color.Alpha * (1 / 255.0f));
    }

    public override string ToString() 
        => FormattableString.Invariant($"{Red}, {Green}, {Blue}, {Alpha}");
}