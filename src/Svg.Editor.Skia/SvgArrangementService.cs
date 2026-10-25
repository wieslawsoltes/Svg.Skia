using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

public enum SvgAlignment { Left, HorizontalCenter, Right, Top, VerticalCenter, Bottom }
public enum SvgArrangementAxis { Horizontal, Vertical }
public enum SvgAlignmentTarget { Selection, Artboard, KeyObject }

/// <summary>
/// Pure picture-space layout planning. Returns one translation per original input;
/// sorting is stable and input bounds are never mutated. Explicit spacing can be
/// negative for deliberate overlap. A key object remains fixed at its original position.
/// </summary>
public static class SvgArrangementService
{
    public static SKPoint[] Align(IReadOnlyList<SKRect> bounds, SKRect reference, SvgAlignment alignment, int fixedIndex = -1)
    {
        Validate(bounds, fixedIndex); Validate(reference);
        if (!Enum.IsDefined(typeof(SvgAlignment), alignment)) throw new ArgumentOutOfRangeException(nameof(alignment));
        var result = new SKPoint[bounds.Count];
        for (var index = 0; index < bounds.Count; index++)
        {
            if (index == fixedIndex) continue;
            var item = bounds[index];
            var dx = alignment switch
            {
                SvgAlignment.Left => (double)reference.Left - item.Left,
                SvgAlignment.Right => (double)reference.Right - item.Right,
                SvgAlignment.HorizontalCenter => ((double)reference.Left + reference.Right - item.Left - item.Right) / 2,
                _ => 0
            };
            var dy = alignment switch
            {
                SvgAlignment.Top => (double)reference.Top - item.Top,
                SvgAlignment.Bottom => (double)reference.Bottom - item.Bottom,
                SvgAlignment.VerticalCenter => ((double)reference.Top + reference.Bottom - item.Top - item.Bottom) / 2,
                _ => 0
            };
            result[index] = Delta(dx, dy);
        }
        return result;
    }

    public static SKPoint[] DistributeCenters(IReadOnlyList<SKRect> bounds, SvgArrangementAxis axis)
    {
        Validate(bounds, -1); Validate(axis);
        var result = new SKPoint[bounds.Count];
        if (bounds.Count < 3) return result;
        var order = Enumerable.Range(0, bounds.Count).OrderBy(index => Center(bounds[index], axis)).ToArray();
        var start = Center(bounds[order[0]], axis); var end = Center(bounds[order[order.Length - 1]], axis);
        for (var position = 1; position < order.Length - 1; position++)
        {
            var index = order[position]; var amount = start + (end - start) * position / (order.Length - 1) - Center(bounds[index], axis);
            result[index] = axis == SvgArrangementAxis.Horizontal ? Delta(amount, 0) : Delta(0, amount);
        }
        return result;
    }

    /// <summary>
    /// A null gap equalizes edge-to-edge spacing within the current outer span, preserving
    /// both outer edges. Explicit gap places objects around fixedIndex, or the first
    /// spatial object when no key is supplied. Automatic gap and a key are incompatible.
    /// </summary>
    public static SKPoint[] DistributeGaps(IReadOnlyList<SKRect> bounds, SvgArrangementAxis axis, double? gap = null, int fixedIndex = -1)
    {
        Validate(bounds, fixedIndex); Validate(axis);
        if (gap is { } spacing && !Finite(spacing)) throw new ArgumentOutOfRangeException(nameof(gap));
        if (gap is null && fixedIndex >= 0) throw new ArgumentException("Choose an explicit gap when keeping a key object fixed.", nameof(gap));
        var result = new SKPoint[bounds.Count];
        if (bounds.Count < 2) return result;
        var order = Enumerable.Range(0, bounds.Count).OrderBy(index => Start(bounds[index], axis)).ToArray();
        var anchor = fixedIndex < 0 ? 0 : Array.IndexOf(order, fixedIndex);
        var distance = gap ?? (End(bounds[order[order.Length - 1]], axis) - Start(bounds[order[0]], axis)
            - order.Sum(index => Length(bounds[index], axis))) / (order.Length - 1);
        var positions = new double[order.Length]; positions[anchor] = Start(bounds[order[anchor]], axis);
        for (var index = anchor + 1; index < order.Length; index++)
            positions[index] = positions[index - 1] + Length(bounds[order[index - 1]], axis) + distance;
        for (var index = anchor - 1; index >= 0; index--)
            positions[index] = positions[index + 1] - distance - Length(bounds[order[index]], axis);
        for (var index = 0; index < order.Length; index++)
        {
            var amount = positions[index] - Start(bounds[order[index]], axis);
            // The two outer positions are exact anchors for automatic spacing; avoid
            // creating microscopic edits from summation roundoff at the final edge.
            if (gap is null && (index == 0 || index == order.Length - 1)) amount = 0;
            result[order[index]] = axis == SvgArrangementAxis.Horizontal ? Delta(amount, 0) : Delta(0, amount);
        }
        return result;
    }

    public static SKRect Union(IReadOnlyList<SKRect> bounds)
    {
        Validate(bounds, -1);
        if (bounds.Count == 0) throw new ArgumentException("At least one object is required.", nameof(bounds));
        return new SKRect(bounds.Min(item => item.Left), bounds.Min(item => item.Top), bounds.Max(item => item.Right), bounds.Max(item => item.Bottom));
    }
    private static double Start(SKRect value, SvgArrangementAxis axis) => axis == SvgArrangementAxis.Horizontal ? value.Left : value.Top;
    private static double End(SKRect value, SvgArrangementAxis axis) => axis == SvgArrangementAxis.Horizontal ? value.Right : value.Bottom;
    private static double Length(SKRect value, SvgArrangementAxis axis) => End(value, axis) - Start(value, axis);
    private static double Center(SKRect value, SvgArrangementAxis axis) => (Start(value, axis) + End(value, axis)) / 2;
    private static SKPoint Delta(double x, double y)
    {
        if (!Finite(x) || !Finite(y) || Math.Abs(x) > float.MaxValue || Math.Abs(y) > float.MaxValue)
            throw new InvalidOperationException("The requested arrangement exceeds finite picture-space coordinates.");
        return new SKPoint((float)x, (float)y);
    }
    private static void Validate(SvgArrangementAxis axis)
    { if (!Enum.IsDefined(typeof(SvgArrangementAxis), axis)) throw new ArgumentOutOfRangeException(nameof(axis)); }
    private static void Validate(IReadOnlyList<SKRect> bounds, int fixedIndex)
    {
        if (bounds is null) throw new ArgumentNullException(nameof(bounds));
        if (fixedIndex < -1 || fixedIndex >= bounds.Count) throw new ArgumentOutOfRangeException(nameof(fixedIndex));
        foreach (var bound in bounds) Validate(bound);
    }
    private static void Validate(SKRect bound)
    {
        if (!Finite(bound.Left) || !Finite(bound.Right) || !Finite(bound.Top) || !Finite(bound.Bottom)
            || bound.Right < bound.Left || bound.Bottom < bound.Top)
            throw new ArgumentException("Arrangement requires finite, non-inverted bounds.");
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
