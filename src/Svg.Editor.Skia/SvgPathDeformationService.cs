using System;
using System.Collections.Generic;
using System.Threading;
using SkiaSharp;

namespace Svg.Editor.Skia;

public enum SvgDeformationKind { Warp, Twirl, Pucker, Bloat, Scallop, Crystallize, Wrinkle }

/// <summary>Brush dimensions are in picture coordinates, independent of object transforms and zoom.</summary>
public sealed class SvgDeformationOptions
{
    public float Radius { get; set; } = 40;
    public float Strength { get; set; } = 0.35f;
    public int Detail { get; set; } = 8;
    public float Flatness { get; set; } = 0.5f;
    public int MaximumPoints { get; set; } = 32768;

    internal void Validate()
    {
        if (!Finite(Radius) || Radius <= 0 || Radius > 1000000)
            throw new ArgumentOutOfRangeException(nameof(Radius));
        if (!Finite(Strength) || Strength < -1 || Strength > 1)
            throw new ArgumentOutOfRangeException(nameof(Strength));
        if (Detail < 2 || Detail > 64)
            throw new ArgumentOutOfRangeException(nameof(Detail));
        if (!Finite(Flatness) || Flatness <= 0 || Flatness > 32)
            throw new ArgumentOutOfRangeException(nameof(Flatness));
        if (MaximumPoints < 16 || MaximumPoints > 131072)
            throw new ArgumentOutOfRangeException(nameof(MaximumPoints));
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>
/// Deterministic brush deformations with finite support and a smooth boundary falloff.
/// Adaptive arclength sampling preserves open/closed contours and fill rules, and resolves
/// curves as well as long straight segments passing through the brush. No input is mutated.
/// </summary>
public static class SvgPathDeformationService
{
    public const int MaximumStrokePoints = 4096;
    private const int MaximumStamps = 2048;
    private const int MaximumContours = 4096;
    private const int MaximumDepth = 24;
    private const int MaximumEvaluations = 8000000;

    private readonly struct Stamp
    {
        public Stamp(SKPoint center, SKPoint delta, float weight) { Center = center; Delta = delta; Weight = weight; }
        public SKPoint Center { get; }
        public SKPoint Delta { get; }
        public float Weight { get; }
    }

    private readonly struct Sample
    {
        public Sample(float distance, SKPoint point) { Distance = distance; Point = point; }
        public float Distance { get; }
        public SKPoint Point { get; }
    }

    /// <summary>The caller owns the returned path. A no-op returns an exact geometry copy.</summary>
    public static SKPath Deform(SKPath source, SvgDeformationKind kind, IReadOnlyList<SKPoint> stroke,
        SvgDeformationOptions? options, out bool changed, CancellationToken cancellationToken = default)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (!Enum.IsDefined(typeof(SvgDeformationKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        (options ??= new SvgDeformationOptions()).Validate();
        ValidateStroke(stroke);
        cancellationToken.ThrowIfCancellationRequested();
        changed = false;
        if (source.IsEmpty || options.Strength == 0 || kind == SvgDeformationKind.Warp && stroke.Count == 1)
            return new SKPath(source);
        var stamps = CreateStamps(stroke, kind, options.Radius);
        if (stamps.Count == 0)
            return new SKPath(source);
        var support = SupportBounds(stroke, options.Radius);
        if (!Overlaps(source.Bounds, support))
            return new SKPath(source);
        var maximumStep = Math.Max(0.00001f, options.Radius / Math.Max(8, options.Detail * 2));
        using var builder = new SKPathBuilder { FillType = source.FillType };
        using var measure = new SKPathMeasure(source, false, 4);
        var pointCount = 0;
        var contourCount = 0;
        var evaluations = 0L;
        var anyChanged = false;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++contourCount > MaximumContours)
                throw new InvalidOperationException("Deform at most 4,096 path contours at a time.");
            var length = measure.Length;
            if (!Finite(length))
                throw new ArgumentException("The path contains nonfinite geometry.", nameof(source));
            if (length <= 0.000001f)
                continue;
            var samples = new List<Sample>();
            var first = At(0);
            Add(first);
            Subdivide(first, At(length), 0);
            for (var index = 0; index < samples.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var original = samples[index].Point;
                var point = original;
                foreach (var stamp in stamps)
                {
                    if (++evaluations > MaximumEvaluations)
                        throw new InvalidOperationException("The deformation exceeds the processing budget. Shorten the stroke or simplify the selected paths.");
                    point = ApplyStamp(point, stamp, kind, options);
                }
                if (DistanceSquared(point, original) > 1e-12)
                    anyChanged = true;
                if (index == 0)
                    builder.MoveTo(point);
                else
                    builder.LineTo(point);
            }
            if (measure.IsClosed)
                builder.Close();

            Sample At(float distance)
            {
                if (!measure.GetPositionAndTangent(distance, out var position, out _))
                    throw new InvalidOperationException("The selected path could not be sampled.");
                ValidatePoint(position);
                return new Sample(distance, position);
            }

            void Add(Sample sample)
            {
                if (++pointCount > options.MaximumPoints)
                    throw new InvalidOperationException("The deformation exceeds its point budget. Increase flatness or simplify the selected paths.");
                samples.Add(sample);
            }

            void Subdivide(Sample start, Sample end, int depth)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var span = end.Distance - start.Distance;
                var distance = start.Distance + span * 0.5f;
                if (distance == start.Distance || distance == end.Distance)
                { Add(end); return; }
                var middle = At(distance);
                var quarter = At(start.Distance + span * 0.25f);
                var lastQuarter = At(start.Distance + span * 0.75f);
                var flat = Distance(middle.Point, Lerp(start.Point, end.Point, 0.5f)) <= options.Flatness
                    && Distance(quarter.Point, Lerp(start.Point, end.Point, 0.25f)) <= options.Flatness
                    && Distance(lastQuarter.Point, Lerp(start.Point, end.Point, 0.75f)) <= options.Flatness
                    && span - Distance(start.Point, end.Point) <= options.Flatness;
                var bounds = Bounds(start.Point, quarter.Point, middle.Point, lastQuarter.Point, end.Point);
                bounds.Inflate(options.Flatness, options.Flatness);
                var resolved = !Overlaps(bounds, support) || span <= maximumStep;
                if (flat && resolved)
                { Add(end); return; }
                if (depth >= MaximumDepth)
                    throw new InvalidOperationException("The deformation cannot resolve this path within its subdivision budget.");
                Subdivide(start, middle, depth + 1);
                Subdivide(middle, end, depth + 1);
            }
        } while (measure.NextContour());
        cancellationToken.ThrowIfCancellationRequested();
        changed = anyChanged;
        return changed ? builder.Detach() : new SKPath(source);
    }

    private static List<Stamp> CreateStamps(IReadOnlyList<SKPoint> stroke, SvgDeformationKind kind, float radius)
    {
        var points = new List<SKPoint> { stroke[0] };
        var spacing = Math.Max(0.00001, radius / 4d);
        var toNext = spacing;
        for (var index = 1; index < stroke.Count; index++)
        {
            var a = stroke[index - 1];
            var b = stroke[index];
            var length = Distance(a, b);
            if (length <= 0.000001)
                continue;
            var consumed = 0d;
            while (length - consumed >= toNext)
            {
                consumed += toNext;
                points.Add(Lerp(a, b, (float)(consumed / length)));
                if (points.Count > MaximumStamps)
                    throw new InvalidOperationException("Shorten the deformation stroke or increase its brush radius.");
                toNext = spacing;
            }
            toNext -= length - consumed;
        }
        var tail = stroke[stroke.Count - 1];
        if (Distance(points[points.Count - 1], tail) > 0.000001)
            points.Add(tail);
        if (points.Count > MaximumStamps)
            throw new InvalidOperationException("Shorten the deformation stroke or increase its brush radius.");
        var stamps = new List<Stamp>(points.Count);
        if (kind != SvgDeformationKind.Warp)
            stamps.Add(new Stamp(points[0], default, 1));
        for (var index = 1; index < points.Count; index++)
        {
            var a = points[index - 1];
            var b = points[index];
            var delta = new SKPoint(b.X - a.X, b.Y - a.Y);
            var weight = (float)Math.Min(1, Distance(a, b) / spacing);
            stamps.Add(new Stamp(kind == SvgDeformationKind.Warp ? Lerp(a, b, 0.5f) : b, delta, weight));
        }
        return stamps;
    }

    private static SKPoint ApplyStamp(SKPoint point, Stamp stamp, SvgDeformationKind kind, SvgDeformationOptions options)
    {
        var x = (double)point.X - stamp.Center.X;
        var y = (double)point.Y - stamp.Center.Y;
        var normalized = (x * x + y * y) / ((double)options.Radius * options.Radius);
        if (normalized >= 1)
            return point;
        var falloff = (1 - normalized) * (1 - normalized);
        var strength = options.Strength * falloff;
        if (kind == SvgDeformationKind.Warp)
            return CheckedPoint(point.X + stamp.Delta.X * strength, point.Y + stamp.Delta.Y * strength);
        strength *= stamp.Weight;
        var angle = Math.Atan2(y, x);
        switch (kind)
        {
            case SvgDeformationKind.Twirl:
                var turn = strength * Math.PI / 2;
                var cosine = Math.Cos(turn);
                var sine = Math.Sin(turn);
                return CheckedPoint(stamp.Center.X + x * cosine - y * sine, stamp.Center.Y + x * sine + y * cosine);
            case SvgDeformationKind.Pucker:
            case SvgDeformationKind.Bloat:
                var factor = Math.Exp((kind == SvgDeformationKind.Pucker ? -1 : 1) * strength * 0.6);
                return CheckedPoint(stamp.Center.X + x * factor, stamp.Center.Y + y * factor);
            case SvgDeformationKind.Scallop:
                factor = 1 - strength * 0.4 * (0.5 + 0.5 * Math.Cos(angle * options.Detail));
                return CheckedPoint(stamp.Center.X + x * factor, stamp.Center.Y + y * factor);
            case SvgDeformationKind.Crystallize:
                var phase = angle * options.Detail / (Math.PI * 2);
                var tooth = 1 - 2 * Math.Abs(phase - Math.Floor(phase + 0.5));
                factor = 1 + strength * 0.45 * tooth;
                return CheckedPoint(stamp.Center.X + x * factor, stamp.Center.Y + y * factor);
            case SvgDeformationKind.Wrinkle:
                var frequency = options.Detail * Math.PI;
                var dx = Math.Sin(y / options.Radius * frequency + 0.6 * Math.Sin(x / options.Radius * Math.PI * 3));
                var dy = Math.Cos(x / options.Radius * frequency + 0.6 * Math.Cos(y / options.Radius * Math.PI * 3));
                return CheckedPoint(point.X + dx * options.Radius * strength * 0.12,
                    point.Y + dy * options.Radius * strength * 0.12);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    internal static void ValidateStroke(IReadOnlyList<SKPoint> stroke)
    {
        if (stroke is null)
            throw new ArgumentNullException(nameof(stroke));
        if (stroke.Count == 0 || stroke.Count > MaximumStrokePoints)
            throw new ArgumentOutOfRangeException(nameof(stroke));
        foreach (var point in stroke)
            ValidatePoint(point);
    }

    private static SKPoint CheckedPoint(double x, double y)
    {
        var point = new SKPoint((float)x, (float)y);
        ValidatePoint(point);
        return point;
    }

    private static void ValidatePoint(SKPoint point)
    {
        if (!Finite(point.X) || !Finite(point.Y))
            throw new ArgumentException("Deformation coordinates must be finite.");
    }

    private static SKRect SupportBounds(IReadOnlyList<SKPoint> stroke, float radius)
    {
        var left = stroke[0].X;
        var top = stroke[0].Y;
        var right = left;
        var bottom = top;
        foreach (var point in stroke)
        { left = Math.Min(left, point.X); right = Math.Max(right, point.X); top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y); }
        var start = CheckedPoint((double)left - radius, (double)top - radius);
        var end = CheckedPoint((double)right + radius, (double)bottom + radius);
        return new SKRect(start.X, start.Y, end.X, end.Y);
    }

    private static SKRect Bounds(SKPoint a, SKPoint b, SKPoint c, SKPoint d, SKPoint e)
        => new(Math.Min(a.X, Math.Min(b.X, Math.Min(c.X, Math.Min(d.X, e.X)))),
            Math.Min(a.Y, Math.Min(b.Y, Math.Min(c.Y, Math.Min(d.Y, e.Y)))),
            Math.Max(a.X, Math.Max(b.X, Math.Max(c.X, Math.Max(d.X, e.X)))),
            Math.Max(a.Y, Math.Max(b.Y, Math.Max(c.Y, Math.Max(d.Y, e.Y)))));

    private static bool Overlaps(SKRect a, SKRect b)
        => a.Left <= b.Right && a.Right >= b.Left && a.Top <= b.Bottom && a.Bottom >= b.Top;
    private static SKPoint Lerp(SKPoint a, SKPoint b, float t)
        => CheckedPoint(a.X + ((double)b.X - a.X) * t, a.Y + ((double)b.Y - a.Y) * t);
    private static double DistanceSquared(SKPoint a, SKPoint b)
        => ((double)a.X - b.X) * ((double)a.X - b.X) + ((double)a.Y - b.Y) * ((double)a.Y - b.Y);
    private static double Distance(SKPoint a, SKPoint b) => Math.Sqrt(DistanceSquared(a, b));
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
