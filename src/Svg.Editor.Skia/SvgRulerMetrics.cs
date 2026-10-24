using System;
using System.Collections.Generic;
using System.Globalization;

namespace Svg.Editor.Skia;

public readonly struct SvgRulerTick
{
    internal SvgRulerTick(double pixel, double value, bool major)
    { Pixel = pixel; Value = value; IsMajor = major; }
    public double Pixel { get; }
    public double Value { get; }
    public bool IsMajor { get; }
    public string Label => Math.Abs(Value) < 1e-9 ? "0" : Value.ToString("0.####", CultureInfo.InvariantCulture);
}

public static class SvgRulerMetrics
{
    /// <summary>Produces at most 512 ticks; origin and scale are in view DIPs, not device pixels.</summary>
    public static IReadOnlyList<SvgRulerTick> Create(double origin, double scale, double length)
    {
        if (!Finite(origin) || !Finite(scale) || !Finite(length) || scale <= 0 || length <= 0) return Array.Empty<SvgRulerTick>();
        var desired = Math.Max(64, length / 100) / scale;
        var power = Math.Pow(10, Math.Floor(Math.Log10(desired)));
        var normalized = desired / power;
        var major = (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * power;
        var minor = major / 5;
        if (!Finite(minor) || minor <= 0) return Array.Empty<SvgRulerTick>();
        var start = Math.Floor(-origin / scale / minor);
        if (!Finite(start) || Math.Abs(start) > 9e15) return Array.Empty<SvgRulerTick>();
        var ticks = new List<SvgRulerTick>();
        for (var index = 0; index < 512; index++)
        {
            var ordinal = start + index;
            var value = ordinal * minor;
            var pixel = origin + value * scale;
            if (pixel > length) break;
            if (pixel >= 0) ticks.Add(new SvgRulerTick(pixel, value, Math.Abs(ordinal % 5) < 0.001));
        }
        return ticks;
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
