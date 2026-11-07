using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>A measured point on one contour, expressed in the input path's coordinate system.</summary>
public readonly struct SvgPathCutPoint
{
    internal SvgPathCutPoint(int contour, float distance, float separation, SKPoint position)
    { Contour = contour; Distance = distance; Separation = separation; Position = position; }
    public int Contour { get; }
    public float Distance { get; }
    public float Separation { get; }
    public SKPoint Position { get; }
}

/// <summary>Geometry shared by cutting-tool previews and atomic SVG edits.</summary>
public static class SvgPathCuttingService
{
    public const int MaximumStrokePoints = 16384;
    private const int MaximumContours = 4096;

    /// <summary>Finds the closest contour point using bounded arclength sampling and local refinement.</summary>
    public static SvgPathCutPoint? FindNearest(SKPath path, SKPoint point, float tolerance)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        ValidatePoint(point);
        if (!Finite(tolerance) || tolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        SvgPathCutPoint? nearest = null;
        var best = (double)tolerance * tolerance;
        using var measure = new SKPathMeasure(path, false, 4);
        var contour = 0;
        do
        {
            if (contour >= MaximumContours)
                throw new InvalidOperationException("Cut paths with at most 4,096 contours at a time.");
            var length = measure.Length;
            if (!Finite(length))
                throw new ArgumentException("The path has invalid coordinates.", nameof(path));
            if (length > 0.000001f)
            {
                var samples = (int)Math.Max(16, Math.Min(4096, Math.Ceiling(length / Math.Max(tolerance / 2, 0.25f))));
                var step = length / samples;
                var sampleDistance = 0f;
                var sampleBest = double.MaxValue;
                for (var index = 0; index <= samples; index++)
                {
                    var distance = index * step;
                    var squared = DistanceSquared(measure, distance, point);
                    if (squared < sampleBest)
                    { sampleBest = squared; sampleDistance = distance; }
                }
                var low = Math.Max(0, sampleDistance - step);
                var high = Math.Min(length, sampleDistance + step);
                for (var iteration = 0; iteration < 28; iteration++)
                {
                    var a = low + (high - low) / 3;
                    var b = high - (high - low) / 3;
                    if (DistanceSquared(measure, a, point) <= DistanceSquared(measure, b, point))
                        high = b;
                    else
                        low = a;
                }
                var candidate = (low + high) / 2;
                if (DistanceSquared(measure, sampleDistance, point) < DistanceSquared(measure, candidate, point))
                    candidate = sampleDistance;
                var separation = DistanceSquared(measure, candidate, point);
                if (separation <= best && measure.GetPositionAndTangent(candidate, out var position, out _))
                {
                    best = separation;
                    nearest = new SvgPathCutPoint(contour, candidate, (float)Math.Sqrt(separation), position);
                }
            }
            contour++;
        } while (measure.NextContour());
        return nearest;
    }

    /// <summary>
    /// Opens a closed contour or splits an open contour while preserving native curves.
    /// Unaffected contours stay in the first piece. The caller owns the returned paths.
    /// An endpoint of an already open contour produces no pieces and no edit.
    /// </summary>
    public static IReadOnlyList<SKPath> SplitAt(SKPath path, SvgPathCutPoint location)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        using var first = new SKPathBuilder();
        using var second = new SKPathBuilder();
        using var measure = new SKPathMeasure(path, false, 4);
        var found = false;
        var hasSecond = false;
        var contour = 0;
        do
        {
            var length = measure.Length;
            if (contour++ >= MaximumContours)
                throw new InvalidOperationException("Cut paths with at most 4,096 contours at a time.");
            if (length <= 0.000001f)
                continue;
            if (contour - 1 != location.Contour)
            {
                using var unchanged = Extract(measure, 0, length);
                first.AddPath(unchanged);
                if (measure.IsClosed)
                    first.Close();
                continue;
            }
            var cut = Math.Max(0, Math.Min(length, location.Distance));
            var epsilon = Math.Max(0.00001f, length * 0.000001f);
            if (!measure.IsClosed && (cut <= epsilon || cut >= length - epsilon))
                return Array.Empty<SKPath>();
            found = true;
            if (measure.IsClosed)
            {
                using var opened = new SKPathBuilder();
                if (cut < length)
                    measure.GetSegment(cut, length, opened, true);
                if (cut > 0)
                    measure.GetSegment(0, cut, opened, cut >= length);
                using var openedPath = opened.Detach();
                first.AddPath(openedPath);
            }
            else
            {
                using var before = Extract(measure, 0, cut);
                using var after = Extract(measure, cut, length);
                first.AddPath(before);
                second.AddPath(after);
                hasSecond = true;
            }
        } while (measure.NextContour());
        if (!found)
            return Array.Empty<SKPath>();
        var result = new List<SKPath> { first.Detach() };
        if (hasSecond)
            result.Add(second.Detach());
        foreach (var piece in result)
            piece.FillType = path.FillType;
        return result;
    }

    /// <summary>Subdivides a native curve at an arclength point without opening its contour.</summary>
    public static SKPath InsertAnchor(SKPath path, SvgPathCutPoint location)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        using var builder = new SKPathBuilder();
        using var measure = new SKPathMeasure(path, false, 4);
        var contour = 0;
        do
        {
            var length = measure.Length;
            if (contour++ >= MaximumContours)
                throw new InvalidOperationException("Edit paths with at most 4,096 contours at a time.");
            if (length <= 0.000001f)
                continue;
            using var segment = new SKPathBuilder();
            if (contour - 1 == location.Contour && location.Distance > 0 && location.Distance < length)
            {
                measure.GetSegment(0, location.Distance, segment, true);
                measure.GetSegment(location.Distance, length, segment, false);
            }
            else
                measure.GetSegment(0, length, segment, true);
            using var segmentPath = segment.Detach();
            builder.AddPath(segmentPath);
            if (measure.IsClosed)
                builder.Close();
        } while (measure.NextContour());
        var result = builder.Detach();
        result.FillType = path.FillType;
        return result;
    }

    /// <summary>Builds the exact round-ended region swept by an eraser, including a single click.</summary>
    public static SKPath CreateSweep(IReadOnlyList<SKPoint> points, float diameter)
    {
        ValidateStroke(points, 1);
        if (!Finite(diameter) || diameter <= 0)
            throw new ArgumentOutOfRangeException(nameof(diameter));
        using var builder = new SKPathBuilder();
        if (points.Count == 1)
        {
            builder.AddCircle(points[0].X, points[0].Y, diameter / 2);
            return builder.Detach();
        }
        using var centerline = new SKPathBuilder();
        centerline.MoveTo(points[0]);
        for (var index = 1; index < points.Count; index++)
            centerline.LineTo(points[index]);
        using var line = centerline.Detach();
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = diameter, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        if (!paint.GetFillPath(line, builder))
            throw new InvalidOperationException("The eraser region could not be calculated.");
        return builder.Detach();
    }

    /// <summary>
    /// Closes one side of an open knife stroke outside the artwork bounds. Intersecting
    /// and subtracting this region partitions artwork without deleting any filled area.
    /// The end tangents extend beyond the artwork, so the resulting edges remain closed.
    /// </summary>
    public static SKPath CreateKnifeRegion(IReadOnlyList<SKPoint> points, SKRect artworkBounds)
    {
        ValidateStroke(points, 2);
        var firstDirection = 1;
        while (firstDirection < points.Count && SKPoint.Distance(points[0], points[firstDirection]) < 0.00001f)
            firstDirection++;
        var lastDirection = points.Count - 2;
        while (lastDirection >= 0 && SKPoint.Distance(points[points.Count - 1], points[lastDirection]) < 0.00001f)
            lastDirection--;
        if (firstDirection == points.Count || lastDirection < 0)
            throw new ArgumentException("Draw a knife stroke with two distinct points.", nameof(points));
        var left = artworkBounds.Left;
        var top = artworkBounds.Top;
        var right = artworkBounds.Right;
        var bottom = artworkBounds.Bottom;
        foreach (var point in points)
        { left = Math.Min(left, point.X); top = Math.Min(top, point.Y); right = Math.Max(right, point.X); bottom = Math.Max(bottom, point.Y); }
        var margin = Math.Max(1, Math.Max(right - left, bottom - top));
        var bounds = new SKRect(left - margin, top - margin, right + margin, bottom + margin);
        ValidatePoint(new SKPoint(bounds.Left, bounds.Top));
        ValidatePoint(new SKPoint(bounds.Right, bounds.Bottom));
        var start = ExtendToBounds(points[0], Subtract(points[0], points[firstDirection]), bounds);
        var end = ExtendToBounds(points[points.Count - 1], Subtract(points[points.Count - 1], points[lastDirection]), bounds);
        using var builder = new SKPathBuilder();
        builder.MoveTo(start);
        foreach (var point in points)
            builder.LineTo(point);
        builder.LineTo(end);
        var perimeter = 2d * (bounds.Width + bounds.Height);
        var endOffset = PerimeterOffset(end, bounds);
        var startOffset = PerimeterOffset(start, bounds);
        if (startOffset <= endOffset)
            startOffset += perimeter;
        var corners = new[] { new SKPoint(bounds.Right, bounds.Top), new SKPoint(bounds.Right, bounds.Bottom),
            new SKPoint(bounds.Left, bounds.Bottom), new SKPoint(bounds.Left, bounds.Top) };
        var distances = new[] { (double)bounds.Width, bounds.Width + (double)bounds.Height, 2d * bounds.Width + bounds.Height, perimeter };
        for (var turn = 0; turn < 2; turn++)
            for (var index = 0; index < corners.Length; index++)
            {
                var distance = distances[index] + turn * perimeter;
                if (distance > endOffset + 0.000001 && distance < startOffset - 0.000001)
                    builder.LineTo(corners[index]);
            }
        builder.LineTo(start);
        builder.Close();
        return builder.Detach();
    }

    private static SKPath Extract(SKPathMeasure measure, float start, float end)
    {
        using var path = new SKPathBuilder();
        if (!measure.GetSegment(start, end, path, true))
            throw new InvalidOperationException("The path segment could not be extracted.");
        return path.Detach();
    }

    private static double DistanceSquared(SKPathMeasure measure, float distance, SKPoint point)
    {
        if (!measure.GetPositionAndTangent(distance, out var position, out _))
            return double.MaxValue;
        var x = (double)position.X - point.X;
        var y = (double)position.Y - point.Y;
        return x * x + y * y;
    }

    private static SKPoint ExtendToBounds(SKPoint origin, SKPoint direction, SKRect bounds)
    {
        var distance = double.MaxValue;
        if (direction.X > 0)
            distance = Math.Min(distance, (bounds.Right - origin.X) / (double)direction.X);
        if (direction.X < 0)
            distance = Math.Min(distance, (bounds.Left - origin.X) / (double)direction.X);
        if (direction.Y > 0)
            distance = Math.Min(distance, (bounds.Bottom - origin.Y) / (double)direction.Y);
        if (direction.Y < 0)
            distance = Math.Min(distance, (bounds.Top - origin.Y) / (double)direction.Y);
        return new SKPoint((float)Math.Max(bounds.Left, Math.Min(bounds.Right, origin.X + distance * direction.X)),
            (float)Math.Max(bounds.Top, Math.Min(bounds.Bottom, origin.Y + distance * direction.Y)));
    }

    private static double PerimeterOffset(SKPoint point, SKRect bounds)
    {
        var epsilon = Math.Max(bounds.Width, bounds.Height) * 0.00001f;
        if (Math.Abs(point.Y - bounds.Top) <= epsilon)
            return point.X - (double)bounds.Left;
        if (Math.Abs(point.X - bounds.Right) <= epsilon)
            return bounds.Width + point.Y - (double)bounds.Top;
        if (Math.Abs(point.Y - bounds.Bottom) <= epsilon)
            return bounds.Width + bounds.Height + bounds.Right - (double)point.X;
        return 2d * bounds.Width + bounds.Height + bounds.Bottom - point.Y;
    }

    private static SKPoint Subtract(SKPoint left, SKPoint right) => new(left.X - right.X, left.Y - right.Y);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void ValidatePoint(SKPoint point)
    { if (!Finite(point.X) || !Finite(point.Y)) throw new ArgumentException("Tool coordinates must be finite."); }
    private static void ValidateStroke(IReadOnlyList<SKPoint> points, int minimum)
    {
        if (points is null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count < minimum || points.Count > MaximumStrokePoints)
            throw new ArgumentException("A tool stroke must contain " + minimum + " to " + MaximumStrokePoints + " points.", nameof(points));
        foreach (var point in points)
            ValidatePoint(point);
    }
}
