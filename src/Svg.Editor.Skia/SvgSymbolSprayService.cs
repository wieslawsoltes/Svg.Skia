using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Editor.Skia;

public enum SvgSymbolBrushKind { Shift, Scrunch, Size, Spin, Stain, Screen, Style }

/// <summary>Native symbol placement and influence settings, measured in picture units.</summary>
public sealed class SvgSymbolBrushOptions
{
    public float Radius { get; set; } = 40;
    public float Size { get; set; } = 24;
    public int Density { get; set; } = 3;
    public float SizeVariation { get; set; } = 0.2f;
    public float RotationVariation { get; set; } = 15;
    public float Strength { get; set; } = 0.35f;
    public int Seed { get; set; } = 1;
    public string? GraphicStyleName { get; set; }

    internal void Validate()
    {
        if (!Finite(Radius) || Radius <= 0 || Radius > 1000000)
            throw new ArgumentOutOfRangeException(nameof(Radius));
        if (!Finite(Size) || Size <= 0 || Size > 1000000)
            throw new ArgumentOutOfRangeException(nameof(Size));
        if (Density < 1 || Density > 100)
            throw new ArgumentOutOfRangeException(nameof(Density));
        if (!Finite(SizeVariation) || SizeVariation < 0 || SizeVariation >= 1)
            throw new ArgumentOutOfRangeException(nameof(SizeVariation));
        if (!Finite(RotationVariation) || RotationVariation < 0 || RotationVariation > 180)
            throw new ArgumentOutOfRangeException(nameof(RotationVariation));
        if (!Finite(Strength) || Strength < 0 || Strength > 1)
            throw new ArgumentOutOfRangeException(nameof(Strength));
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public readonly struct SvgSymbolPlacement
{
    internal SvgSymbolPlacement(SKPoint position, float size, float rotation)
    { Position = position; Size = size; Rotation = rotation; }
    public SKPoint Position { get; }
    public float Size { get; }
    public float Rotation { get; }
}

/// <summary>Deterministic placement shared by the canvas's transient preview and committed uses.</summary>
public static class SvgSymbolSprayService
{
    public const int MaximumInstances = 4096;

    public static IReadOnlyList<SvgSymbolPlacement> CreatePlacements(IReadOnlyList<SKPoint> centers, SvgSymbolBrushOptions? options = null)
    {
        if (centers is null)
            throw new ArgumentNullException(nameof(centers));
        options ??= new SvgSymbolBrushOptions();
        options.Validate();
        if ((long)centers.Count * options.Density > MaximumInstances)
            throw new InvalidOperationException("Complete this spray before placing more than 4,096 symbol instances.");
        var random = new Random(options.Seed);
        var result = new List<SvgSymbolPlacement>(centers.Count * options.Density);
        foreach (var center in centers)
        {
            if (float.IsNaN(center.X) || float.IsInfinity(center.X) || float.IsNaN(center.Y) || float.IsInfinity(center.Y))
                throw new ArgumentException("Symbol coordinates must be finite.", nameof(centers));
            for (var index = 0; index < options.Density; index++)
            {
                var distance = Math.Sqrt(random.NextDouble()) * options.Radius;
                var angle = random.NextDouble() * Math.PI * 2;
                var position = new SKPoint(center.X + (float)(Math.Cos(angle) * distance), center.Y + (float)(Math.Sin(angle) * distance));
                var size = options.Size * (1 + (float)(random.NextDouble() * 2 - 1) * options.SizeVariation);
                var rotation = (float)(random.NextDouble() * 2 - 1) * options.RotationVariation;
                result.Add(new SvgSymbolPlacement(position, size, rotation));
            }
        }
        return result;
    }
}
