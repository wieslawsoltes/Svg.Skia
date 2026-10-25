using System;
using SkiaSharp;

namespace Svg.Editor.Skia;

public readonly struct SvgCmykColor
{
    public SvgCmykColor(double cyan, double magenta, double yellow, double black)
        => (Cyan, Magenta, Yellow, Black) = (Clamp(cyan), Clamp(magenta), Clamp(yellow), Clamp(black));
    public double Cyan { get; }
    public double Magenta { get; }
    public double Yellow { get; }
    public double Black { get; }
    public double TotalInk => Cyan + Magenta + Yellow + Black;

    public static SvgCmykColor FromSrgb(SKColor color, double blackGeneration = 1, double totalInkLimit = 3)
    {
        if (double.IsNaN(blackGeneration) || blackGeneration < 0 || blackGeneration > 1 ||
            double.IsNaN(totalInkLimit) || totalInkLimit < 1 || totalInkLimit > 4)
            throw new ArgumentOutOfRangeException(nameof(blackGeneration));
        var alpha = color.Alpha / 255.0;
        var c = (1 - color.Red / 255.0) * alpha;
        var m = (1 - color.Green / 255.0) * alpha;
        var y = (1 - color.Blue / 255.0) * alpha;
        var k = Math.Min(c, Math.Min(m, y)) * blackGeneration;
        var denominator = 1 - k;
        return (denominator < 1e-12 ? new SvgCmykColor(0, 0, 0, 1)
            : new SvgCmykColor((c - k) / denominator, (m - k) / denominator, (y - k) / denominator, k)).LimitInk(totalInkLimit);
    }

    public SvgCmykColor LimitInk(double limit)
    {
        if (double.IsNaN(limit) || limit < 1 || limit > 4)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (TotalInk <= limit)
            return this;
        var factor = (limit - Black) / (Cyan + Magenta + Yellow);
        return new SvgCmykColor(Cyan * factor, Magenta * factor, Yellow * factor, Black);
    }

    internal static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Min(1, Math.Max(0, value));
    internal byte Channel(int channel) => (byte)Math.Round(255 * (channel switch { 0 => Cyan, 1 => Magenta, 2 => Yellow, _ => Black }));
}
