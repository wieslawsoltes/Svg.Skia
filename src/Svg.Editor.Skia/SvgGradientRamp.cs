using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>A gradient stop. Color alpha and SVG stop opacity are independent.</summary>
public sealed class SvgGradientColorStop
{
    internal SvgGradientColorStop(Guid id, double offset, SKColor color, double opacity)
    { Id = id; Offset = offset; Color = color; Opacity = opacity; }
    public Guid Id { get; }
    public double Offset { get; }
    public SKColor Color { get; }
    public double Opacity { get; }
    public SKColor PreviewColor => Color.WithAlpha((byte)Math.Round(Color.Alpha * Opacity));
}

/// <summary>
/// Immutable gradient-ramp value. All editing operations validate before returning a new
/// snapshot. Equal-offset stops retain order, allowing deliberate hard transitions.
/// Stop identities survive moves, reversals and undo; XML IDs belong to the document,
/// not this detached authoring model.
/// </summary>
public sealed class SvgGradientRamp
{
    public const int MaximumStops = 256;
    private readonly ReadOnlyCollection<SvgGradientColorStop> _stops;
    public IReadOnlyList<SvgGradientColorStop> Stops => _stops;

    public SvgGradientRamp(IEnumerable<(double Offset, SKColor Color, double Opacity)> stops)
    {
        if (stops is null) throw new ArgumentNullException(nameof(stops));
        var items = stops.Take(MaximumStops + 1).Select(stop =>
        {
            Unit(stop.Offset, nameof(stops)); Unit(stop.Opacity, nameof(stops));
            return new SvgGradientColorStop(Guid.NewGuid(), stop.Offset, stop.Color, stop.Opacity);
        }).ToArray();
        if (items.Length < 2 || items.Length > MaximumStops)
            throw new ArgumentException($"A gradient ramp requires 2–{MaximumStops} stops.", nameof(stops));
        _stops = Array.AsReadOnly(items.OrderBy(stop => stop.Offset).ToArray());
    }
    private SvgGradientRamp(IEnumerable<SvgGradientColorStop> stops)
        => _stops = Array.AsReadOnly(stops.ToArray());

    public static SvgGradientRamp Default { get; } = new(new[]
    {
        (0d, new SKColor(112, 97, 220), 1d),
        (1d, new SKColor(242, 185, 158), 1d)
    });

    public SvgGradientColorStop Find(Guid id)
        => _stops.FirstOrDefault(stop => stop.Id == id)
            ?? throw new ArgumentException("The selected gradient stop no longer exists.", nameof(id));

    public SvgGradientRamp SetOffset(Guid id, double offset)
    {
        Unit(offset, nameof(offset));
        var original = Find(id);
        if (original.Offset == offset) return this;
        return new SvgGradientRamp(_stops.Select(stop => stop.Id == id
            ? new SvgGradientColorStop(id, offset, stop.Color, stop.Opacity) : stop).OrderBy(stop => stop.Offset));
    }
    public SvgGradientRamp SetColor(Guid id, SKColor color)
    {
        var original = Find(id);
        if (original.Color == color) return this;
        return new SvgGradientRamp(_stops.Select(stop => stop.Id == id
            ? new SvgGradientColorStop(id, stop.Offset, color, stop.Opacity) : stop));
    }
    public SvgGradientRamp SetOpacity(Guid id, double opacity)
    {
        Unit(opacity, nameof(opacity));
        var original = Find(id);
        if (original.Opacity == opacity) return this;
        return new SvgGradientRamp(_stops.Select(stop => stop.Id == id
            ? new SvgGradientColorStop(id, stop.Offset, stop.Color, opacity) : stop));
    }
    public SvgGradientRamp Add(double offset, SKColor color, double opacity, out Guid id)
    {
        Unit(offset, nameof(offset)); Unit(opacity, nameof(opacity));
        if (_stops.Count >= MaximumStops) throw new InvalidOperationException($"A ramp supports at most {MaximumStops} stops.");
        id = Guid.NewGuid();
        var added = new SvgGradientColorStop(id, offset, color, opacity);
        return new SvgGradientRamp(_stops.Concat(new[] { added }).OrderBy(stop => stop.Offset));
    }
    public SvgGradientRamp Duplicate(Guid sourceId, out Guid newId)
    {
        var source = Find(sourceId);
        if (_stops.Count >= MaximumStops) throw new InvalidOperationException($"A ramp supports at most {MaximumStops} stops.");
        newId = Guid.NewGuid();
        var index = _stops.IndexOf(source);
        var items = _stops.ToList();
        items.Insert(index + 1, new SvgGradientColorStop(newId, source.Offset, source.Color, source.Opacity));
        return new SvgGradientRamp(items);
    }
    public SvgGradientRamp Remove(Guid id)
    {
        _ = Find(id);
        if (_stops.Count <= 2) throw new InvalidOperationException("Keep at least two stops in an editable gradient.");
        return new SvgGradientRamp(_stops.Where(stop => stop.Id != id));
    }
    public SvgGradientRamp Reverse()
        => new(_stops.Reverse().Select(stop => new SvgGradientColorStop(stop.Id, 1 - stop.Offset, stop.Color, stop.Opacity)));
    public SvgGradientRamp DistributeEvenly()
        => new(_stops.Select((stop, index) => new SvgGradientColorStop(stop.Id, index / (double)(_stops.Count - 1), stop.Color, stop.Opacity)));

    /// <summary>
    /// Samples unassociated sRGB channels and opacity for a newly inserted stop.
    /// Existing geometry and stops are not changed. Equal offsets select the last stop.
    /// This convenience sampler does not emulate color-managed or linearRGB interpolation.
    /// </summary>
    public (SKColor Color, double Opacity) Sample(double offset)
    {
        Unit(offset, nameof(offset));
        if (offset < _stops[0].Offset) return (_stops[0].Color, _stops[0].Opacity);
        var left = _stops[0];
        for (var index = 1; index < _stops.Count; index++)
        {
            var right = _stops[index];
            if (offset < right.Offset)
            {
                var t = (offset - left.Offset) / (right.Offset - left.Offset);
                byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * t);
                return (new SKColor(Mix(left.Color.Red, right.Color.Red), Mix(left.Color.Green, right.Color.Green),
                    Mix(left.Color.Blue, right.Color.Blue), Mix(left.Color.Alpha, right.Color.Alpha)),
                    left.Opacity + (right.Opacity - left.Opacity) * t);
            }
            left = right;
        }
        return (left.Color, left.Opacity);
    }

    private static void Unit(double value, string parameter)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(parameter, "Use a finite value from zero to one.");
    }
}
