using System;

namespace ShimSkiaSharp;

public readonly struct SKColorF(float red, float green, float blue, float alpha)
{
    public float Red { get; } = red;
    public float Green { get; } = green;
    public float Blue { get; } = blue;
    public float Alpha { get; } = alpha;

    public static readonly SKColorF Empty = default;

    public static implicit operator SKColor(SKColorF color)
    {
        return new(
            (byte)(color.Red * 255.0f),
            (byte)(color.Green * 255.0f),
            (byte)(color.Blue * 255.0f),
            (byte)(color.Alpha * 255.0f));
    }

    public override string ToString() 
        => FormattableString.Invariant($"{Red}, {Green}, {Blue}, {Alpha}");
}