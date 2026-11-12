using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using Svg.Editor.Svg.Models;

namespace Svg.Editor.Skia;

/// <summary>Bounded, native-curve geometry for local path refinement tools.</summary>
public static class SvgPathRefinementService
{
    private const int MaximumSegments = 32768;
    private const int MaximumSamples = 131072;

    /// <summary>Uses the existing width-profile engine to create a round filled brush stroke.</summary>
    public static SKPath CreateBlobOutline(IReadOnlyList<SKPoint> points, float diameter)
    {
        _ = new StrokeInfluence(points, diameter / 2);
        if (points.Count == 0)
            return new SKPath();
        var distinct = new List<ShimSkiaSharp.SKPoint>();
        foreach (var point in points)
            if (distinct.Count == 0 || Math.Abs(distinct[distinct.Count - 1].X - point.X) + Math.Abs(distinct[distinct.Count - 1].Y - point.Y) > 0.00001f)
                distinct.Add(new ShimSkiaSharp.SKPoint(point.X, point.Y));
        if (distinct.Count == 1)
            return SvgPathCuttingService.CreateSweep(points, diameter);
        var path = new SvgPath { PathData = PathService.MakeSmooth(distinct) };
        using var centerline = PathService.ElementToPath(path)!;
        var profile = new StrokeProfile();
        profile.Points.Clear();
        profile.Points.Add(new StrokePointInfo { Offset = 0, Width = diameter });
        profile.Points.Add(new StrokePointInfo { Offset = 1, Width = diameter });
        return SvgVariableStrokeService.CreateOutline(centerline, profile, SKStrokeCap.Round, Math.Max(0.05f, Math.Min(0.5f, diameter / 32)));
    }

    /// <summary>
    /// Smooths anchors touched by a brush without moving open endpoints or rewriting
    /// untouched segments. The returned path is owned by the caller; null is a no-op.
    /// </summary>
    public static SKPath? Smooth(SKPath source, IReadOnlyList<SKPoint> stroke, float radius, float strength = 0.5f)
        => Smooth(source, new StrokeInfluence(stroke, radius), strength);

    internal static SKPath? Smooth(SKPath source, StrokeInfluence influence, float strength)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (!Finite(strength) || strength < 0 || strength > 1)
            throw new ArgumentOutOfRangeException(nameof(strength));
        if (strength == 0 || influence.IsEmpty || !influence.Overlaps(source.Bounds))
            return null;
        using var builder = new SKPathBuilder();
        var changed = false;
        foreach (var contour in ReadContours(source))
        {
            var count = contour.Segments.Count;
            var anchorCount = contour.Closed ? count : count + 1;
            if (anchorCount < 3)
            { AppendContour(builder, contour); continue; }
            var anchors = new SKPoint[anchorCount];
            var positions = new SKPoint[anchorCount];
            var weights = new float[anchorCount];
            anchors[0] = contour.Start;
            for (var index = 1; index < anchorCount; index++)
                anchors[index] = contour.Segments[index - 1].End;
            for (var index = 0; index < anchorCount; index++)
            {
                var openEnd = !contour.Closed && (index == 0 || index == anchorCount - 1);
                var weight = openEnd ? 0 : strength * influence.At(anchors[index]);
                weights[index] = weight;
                var previous = anchors[(index + anchorCount - 1) % anchorCount];
                var next = anchors[(index + 1) % anchorCount];
                positions[index] = Lerp(anchors[index], Lerp(anchors[index], Lerp(previous, next, 0.5f), 0.5f), weight);
            }
            builder.MoveTo(positions[0]);
            for (var index = 0; index < count; index++)
            {
                var segment = contour.Segments[index];
                var endIndex = (index + 1) % anchorCount;
                var a = positions[index];
                var b = positions[endIndex];
                var startWeight = weights[index];
                var endWeight = weights[endIndex];
                if (startWeight == 0 && endWeight == 0)
                { AppendSegment(builder, segment); continue; }
                segment.GetCubicControls(out var originalFirst, out var originalSecond);
                var first = Add(originalFirst, Subtract(a, segment.Start));
                var second = Add(originalSecond, Subtract(b, segment.End));
                var previous = !contour.Closed && index == 0 ? a : positions[(index + anchorCount - 1) % anchorCount];
                var next = !contour.Closed && endIndex == anchorCount - 1 ? b : positions[(endIndex + 1) % anchorCount];
                first = Lerp(first, Add(a, Multiply(Subtract(b, previous), 1f / 6)), startWeight);
                second = Lerp(second, Subtract(b, Multiply(Subtract(next, a), 1f / 6)), endWeight);
                var modified = !Near(a, segment.Start) || !Near(b, segment.End) || !Near(first, originalFirst) || !Near(second, originalSecond);
                if (modified)
                { builder.CubicTo(first, second, b); changed = true; }
                else
                    AppendSegment(builder, segment);
            }
            if (contour.Closed)
                builder.Close();
        }
        if (!changed)
            return null;
        var result = builder.Detach();
        result.FillType = source.FillType;
        return result;
    }

    /// <summary>
    /// Removes centerline intervals inside a circular brush sweep, retaining exact
    /// curve segments, native stroke styling and unhit compound contours.
    /// </summary>
    public static IReadOnlyList<SKPath>? Erase(SKPath source, IReadOnlyList<SKPoint> stroke, float radius)
    {
        var sampleBudget = MaximumSamples;
        return Erase(source, new StrokeInfluence(stroke, radius), ref sampleBudget);
    }

    internal static IReadOnlyList<SKPath>? Erase(SKPath source, StrokeInfluence influence, ref int sampleBudget)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (influence.IsEmpty || !influence.Overlaps(source.Bounds))
            return null;
        using var measure = new SKPathMeasure(source, false, 2);
        using var untouched = new SKPathBuilder();
        var results = new List<SKPath>();
        var changed = false;
        var contours = 0;
        try
        {
            do
            {
                if (++contours > 4096)
                    throw new InvalidOperationException("Refine fewer path contours at a time.");
                var length = measure.Length;
                if (!Finite(length))
                    throw new ArgumentException("The path contains invalid geometry.", nameof(source));
                if (length <= 0.00001f)
                    continue;
                var divisions = Math.Max(2, (int)Math.Min(MaximumSamples + 1d, Math.Ceiling(length / Math.Max(0.01f, Math.Min(0.5f, influence.Radius / 8)))));
                if ((sampleBudget -= divisions) < 0)
                    throw new InvalidOperationException("The path is too long for this brush size. Increase the diameter or refine a smaller selection.");
                var ranges = new List<(float Start, float End)>();
                var inside = influence.Contains(measure.GetPosition(0));
                var hit = inside;
                var start = 0f;
                var previousDistance = 0f;
                for (var index = 1; index <= divisions; index++)
                {
                    var distance = length * index / divisions;
                    var nextInside = influence.Contains(measure.GetPosition(distance));
                    hit |= nextInside;
                    if (nextInside != inside)
                    {
                        var low = previousDistance;
                        var high = distance;
                        for (var iteration = 0; iteration < 24 && high - low > 0.00001f; iteration++)
                        {
                            var middle = (low + high) / 2;
                            if (influence.Contains(measure.GetPosition(middle)) == inside)
                                low = middle;
                            else
                                high = middle;
                        }
                        var boundary = (low + high) / 2;
                        if (!inside && boundary - start > 0.00001f)
                            ranges.Add((start, boundary));
                        if (!nextInside)
                            start = boundary;
                    }
                    inside = nextInside;
                    previousDistance = distance;
                }
                if (!inside && length - start > 0.00001f)
                    ranges.Add((start, length));
                if (!hit)
                {
                    measure.GetSegment(0, length, untouched, true);
                    if (measure.IsClosed)
                        untouched.Close();
                    continue;
                }
                changed = true;
                // The old closed seam is not an erased gap. Keep its two surviving
                // intervals connected so a single brush crossing produces one path.
                if (measure.IsClosed && ranges.Count > 1 && ranges[0].Start == 0 && ranges[ranges.Count - 1].End == length)
                {
                    using var wrapped = new SKPathBuilder();
                    measure.GetSegment(ranges[ranges.Count - 1].Start, length, wrapped, true);
                    measure.GetSegment(0, ranges[0].End, wrapped, false);
                    results.Add(wrapped.Detach());
                    ranges.RemoveAt(ranges.Count - 1);
                    ranges.RemoveAt(0);
                }
                foreach (var range in ranges)
                {
                    using var piece = new SKPathBuilder();
                    if (measure.GetSegment(range.Start, range.End, piece, true))
                        results.Add(piece.Detach());
                }
            }
            while (measure.NextContour());
            if (!changed)
                return null;
            using var unhit = untouched.Detach();
            if (!unhit.IsEmpty)
            {
                using var compound = new SKPathBuilder();
                compound.AddPath(unhit);
                if (results.Count > 0)
                { compound.AddPath(results[0]); results[0].Dispose(); results[0] = compound.Detach(); }
                else
                    results.Add(compound.Detach());
            }
            foreach (var path in results)
                path.FillType = source.FillType;
            var result = results.ToArray();
            results.Clear();
            return result;
        }
        finally { foreach (var path in results) path.Dispose(); }
    }

    internal sealed class Contour
    {
        internal Contour(SKPoint start) { Start = start; }
        internal SKPoint Start { get; }
        internal List<Segment> Segments { get; } = new();
        internal bool Closed { get; set; }
        internal SKPoint End => Segments.Count == 0 ? Start : Segments[Segments.Count - 1].End;
    }

    internal readonly struct Segment
    {
        internal Segment(SKPathVerb verb, SKPoint start, SKPoint end, SKPoint first = default, SKPoint second = default, float weight = 1)
        { Verb = verb; Start = start; End = end; First = first; Second = second; Weight = weight; }
        internal SKPathVerb Verb { get; }
        internal SKPoint Start { get; }
        internal SKPoint End { get; }
        internal SKPoint First { get; }
        internal SKPoint Second { get; }
        internal float Weight { get; }
        internal void GetCubicControls(out SKPoint first, out SKPoint second)
        {
            if (Verb == SKPathVerb.Cubic)
            { first = First; second = Second; }
            else if (Verb is SKPathVerb.Quad or SKPathVerb.Conic)
            { first = Lerp(Start, First, 2 * Weight / 3); second = Lerp(End, First, 2 * Weight / 3); }
            else
            { first = Lerp(Start, End, 1f / 3); second = Lerp(Start, End, 2f / 3); }
        }
    }

    internal static List<Contour> ReadContours(SKPath source)
    {
        var result = new List<Contour>();
        Contour? contour = null;
        var count = 0;
        using var iterator = source.CreateRawIterator();
        var points = new SKPoint[4];
        for (var verb = iterator.Next(points); verb != SKPathVerb.Done; verb = iterator.Next(points))
        {
            if (++count > MaximumSegments)
                throw new InvalidOperationException("Refine a path with fewer than 32,768 segments.");
            if (verb == SKPathVerb.Move)
            { contour = new Contour(points[0]); result.Add(contour); }
            else if (contour is not null)
                switch (verb)
                {
                    case SKPathVerb.Line:
                        contour.Segments.Add(new Segment(verb, points[0], points[1]));
                        break;
                    case SKPathVerb.Quad:
                        contour.Segments.Add(new Segment(verb, points[0], points[2], points[1]));
                        break;
                    case SKPathVerb.Conic:
                        contour.Segments.Add(new Segment(verb, points[0], points[2], points[1], weight: iterator.ConicWeight()));
                        break;
                    case SKPathVerb.Cubic:
                        contour.Segments.Add(new Segment(verb, points[0], points[3], points[1], points[2]));
                        break;
                    case SKPathVerb.Close:
                        if (!Near(contour.End, contour.Start))
                            contour.Segments.Add(new Segment(SKPathVerb.Line, contour.End, contour.Start));
                        contour.Closed = true;
                        break;
                }
        }
        return result;
    }

    internal static void AppendContour(SKPathBuilder builder, Contour contour, bool reverse = false, bool move = true)
    {
        if (move)
            builder.MoveTo(reverse ? contour.End : contour.Start);
        if (!reverse)
        foreach (var segment in contour.Segments)
            AppendSegment(builder, segment);
        else
        for (var index = contour.Segments.Count - 1; index >= 0; index--)
            AppendSegment(builder, contour.Segments[index], true);
        if (contour.Closed)
            builder.Close();
    }

    private static void AppendSegment(SKPathBuilder builder, Segment segment, bool reverse = false)
    {
        var end = reverse ? segment.Start : segment.End;
        switch (segment.Verb)
        {
            case SKPathVerb.Cubic:
                builder.CubicTo(reverse ? segment.Second : segment.First, reverse ? segment.First : segment.Second, end);
                break;
            case SKPathVerb.Quad:
                builder.QuadTo(segment.First, end);
                break;
            case SKPathVerb.Conic:
                builder.ConicTo(segment.First, end, segment.Weight);
                break;
            default:
                builder.LineTo(end);
                break;
        }
    }

    /// <summary>Spatial indexing makes local brush work independent of distant stroke samples.</summary>
    internal sealed class StrokeInfluence
    {
        private readonly IReadOnlyList<SKPoint> _points;
        private readonly Dictionary<(long X, long Y), List<int>> _cells = new();
        private readonly float _radius;
        private readonly SKRect _bounds;
        internal bool IsEmpty => _points.Count == 0;
        internal float Radius => _radius;
        internal StrokeInfluence(IReadOnlyList<SKPoint> points, float radius)
        {
            if (points is null)
                throw new ArgumentNullException(nameof(points));
            if (points.Count > 4096)
                throw new ArgumentException("Use at most 4,096 brush samples per gesture.", nameof(points));
            if (!Finite(radius) || radius < 0.001f || radius > 1000000)
                throw new ArgumentOutOfRangeException(nameof(radius));
            foreach (var point in points)
            if (!Finite(point.X) || !Finite(point.Y) || Math.Abs((double)point.X / radius) > 1e12 || Math.Abs((double)point.Y / radius) > 1e12)
                throw new ArgumentException("Brush coordinates are outside the supported range.", nameof(points));
            _points = points;
            _radius = radius;
            var entries = 0;
            _bounds = points.Count == 0 ? default : new SKRect(points.Min(point => point.X) - radius, points.Min(point => point.Y) - radius,
                points.Max(point => point.X) + radius, points.Max(point => point.Y) + radius);
            for (var index = 0; index < points.Count; index++)
            {
                var a = points[index];
                var b = index == 0 ? a : points[index - 1];
                var left = Cell(Math.Min(a.X, b.X) - radius);
                var right = Cell(Math.Max(a.X, b.X) + radius);
                var top = Cell(Math.Min(a.Y, b.Y) - radius);
                var bottom = Cell(Math.Max(a.Y, b.Y) + radius);
                if ((double)(right - left + 1) * (bottom - top + 1) + entries > 262144)
                    throw new InvalidOperationException("The brush stroke covers too much area. Use a larger brush or a shorter stroke.");
                for (var x = left; x <= right; x++)
                for (var y = top; y <= bottom; y++)
                {
                    if (!_cells.TryGetValue((x, y), out var list))
                        _cells.Add((x, y), list = new List<int>());
                    list.Add(index);
                    entries++;
                }
            }
        }
        internal bool Contains(SKPoint point) => Distance(point) <= _radius;
        internal bool Overlaps(SKRect bounds) => !IsEmpty && bounds.Left <= _bounds.Right && bounds.Right >= _bounds.Left && bounds.Top <= _bounds.Bottom && bounds.Bottom >= _bounds.Top;
        internal float At(SKPoint point)
        {
            var distance = Distance(point);
            if (distance >= _radius)
                return 0;
            var amount = 1 - distance / _radius;
            return amount * amount;
        }
        private float Distance(SKPoint point)
        {
            if (!_cells.TryGetValue((Cell(point.X), Cell(point.Y)), out var indices))
                return float.PositiveInfinity;
            var distance = float.PositiveInfinity;
            foreach (var index in indices)
            {
                var a = _points[index];
                var b = index == 0 ? a : _points[index - 1];
                var delta = Subtract(b, a);
                var denominator = (double)delta.X * delta.X + (double)delta.Y * delta.Y;
                var offset = denominator == 0 ? 0 : Math.Max(0, Math.Min(1, ((double)(point.X - a.X) * delta.X + (double)(point.Y - a.Y) * delta.Y) / denominator));
                distance = Math.Min(distance, SKPoint.Distance(point, Lerp(a, b, (float)offset)));
            }
            return distance;
        }
        private long Cell(float value) => (long)Math.Floor(value / (double)_radius);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Near(SKPoint a, SKPoint b) => Math.Abs(a.X - b.X) < 0.00001f && Math.Abs(a.Y - b.Y) < 0.00001f;
    private static SKPoint Lerp(SKPoint a, SKPoint b, float amount) => new(a.X + (b.X - a.X) * amount, a.Y + (b.Y - a.Y) * amount);
    private static SKPoint Add(SKPoint a, SKPoint b) => new(a.X + b.X, a.Y + b.Y);
    private static SKPoint Subtract(SKPoint a, SKPoint b) => new(a.X - b.X, a.Y - b.Y);
    private static SKPoint Multiply(SKPoint value, float scale) => new(value.X * scale, value.Y * scale);
}
