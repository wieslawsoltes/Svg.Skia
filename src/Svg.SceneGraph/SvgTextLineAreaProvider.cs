using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Skia;

internal enum SvgTextLineAreaFlow
{
    HorizontalLeftToRight,
    HorizontalRightToLeft,
    VerticalRightToLeftColumns,
    VerticalLeftToRightColumns
}

internal readonly record struct SvgTextLineShapeSample(
    SKPoint Point,
    float Distance,
    bool StartsSubpath,
    bool ClosesSubpath);

internal readonly record struct SvgTextLineAreaFragment(float Start, float End)
{
    public float InlineSize => End - Start;
}

internal readonly record struct SvgTextLineArea(
    float Start,
    float InlineSize,
    float BlockCoordinate,
    SvgTextLineAreaFragment[] Fragments,
    int ShapeIndex,
    int InlineProgression = 1);

internal readonly record struct SvgTextLineShape(
    SKRect Bounds,
    SvgTextLineShapeSample[] Samples,
    float Padding = 0f,
    float Margin = 0f,
    SKPathFillType FillType = SKPathFillType.Winding)
{
    public SKRect EffectiveBounds
    {
        get
        {
            var inset = Padding - Margin;
            var bounds = new SKRect(
                Bounds.Left + inset,
                Bounds.Top + inset,
                Bounds.Right - inset,
                Bounds.Bottom - inset);
            return bounds.Right > bounds.Left && bounds.Bottom > bounds.Top
                ? bounds
                : SKRect.Empty;
        }
    }

    public List<SvgTextLineAreaFragment> ResolveHorizontalIntervals(float lineTop, float lineBottom, float tolerance, bool includeAnyBlockOverlap = false)
    {
        var intervals = new List<SvgTextLineAreaFragment>();
        var bounds = EffectiveBounds;
        if (bounds.Width <= 0f ||
            bounds.Height <= 0f ||
            lineBottom <= bounds.Top ||
            lineTop >= bounds.Bottom)
        {
            return intervals;
        }

        if (Samples.Length < 2)
        {
            intervals.Add(new SvgTextLineAreaFragment(bounds.Left, bounds.Right));
            return intervals;
        }

        var sampleCoordinates = GetLineBandSampleCoordinates(lineTop, lineBottom, bounds.Top, bounds.Bottom);
        if (sampleCoordinates.Count == 0)
        {
            return intervals;
        }

        intervals = ResolveHorizontalIntervalsAt(sampleCoordinates[0], bounds, tolerance);
        for (var i = 1; i < sampleCoordinates.Count; i++)
        {
            var sampleIntervals = ResolveHorizontalIntervalsAt(sampleCoordinates[i], bounds, tolerance);
            if (includeAnyBlockOverlap)
            {
                intervals.AddRange(sampleIntervals);
                continue;
            }

            IntersectIntervals(intervals, sampleIntervals, tolerance);
            if (intervals.Count == 0)
            {
                return intervals;
            }
        }

        NormalizeIntervals(intervals);
        return intervals;
    }

    private List<SvgTextLineAreaFragment> ResolveHorizontalIntervalsAt(float lineCenterY, SKRect bounds, float tolerance)
    {
        var intervals = new List<SvgTextLineAreaFragment>();
        var intersections = new List<(float Coordinate, int WindingDelta)>();
        for (var i = 1; i < Samples.Length; i++)
        {
            if (Samples[i].StartsSubpath)
            {
                continue;
            }

            var start = Samples[i - 1].Point;
            var end = Samples[i].Point;
            if (Math.Abs(end.Y - start.Y) <= 0.0001f)
            {
                continue;
            }

            if (!((start.Y <= lineCenterY && end.Y > lineCenterY) ||
                  (end.Y <= lineCenterY && start.Y > lineCenterY)))
            {
                continue;
            }

            var t = (lineCenterY - start.Y) / (end.Y - start.Y);
            intersections.Add((start.X + ((end.X - start.X) * t), end.Y > start.Y ? 1 : -1));
        }

        intersections.Sort(static (left, right) =>
        {
            var coordinateComparison = left.Coordinate.CompareTo(right.Coordinate);
            return coordinateComparison != 0
                ? coordinateComparison
                : left.WindingDelta.CompareTo(right.WindingDelta);
        });

        var edgeInset = Padding - Margin;
        if (FillType == SKPathFillType.EvenOdd)
        {
            for (var i = 0; i + 1 < intersections.Count; i += 2)
            {
                AddInterval(intersections[i].Coordinate, intersections[i + 1].Coordinate);
            }

            return intervals;
        }

        var winding = 0;
        float? startX = null;
        for (var i = 0; i < intersections.Count;)
        {
            var coordinate = intersections[i].Coordinate;
            var wasInside = winding != 0;
            while (i < intersections.Count &&
                   Math.Abs(intersections[i].Coordinate - coordinate) <= 0.0001f)
            {
                winding += intersections[i].WindingDelta;
                i++;
            }

            var isInside = winding != 0;
            if (!wasInside && isInside)
            {
                startX = coordinate;
            }
            else if (wasInside && !isInside && startX.HasValue)
            {
                AddInterval(startX.Value, coordinate);
                startX = null;
            }
        }

        return intervals;

        void AddInterval(float rawLeft, float rawRight)
        {
            var left = Math.Max(bounds.Left, Math.Min(rawLeft, rawRight) + edgeInset);
            var right = Math.Min(bounds.Right, Math.Max(rawLeft, rawRight) - edgeInset);
            if (right > left + tolerance)
            {
                intervals.Add(new SvgTextLineAreaFragment(left, right));
            }
        }
    }

    public List<SvgTextLineAreaFragment> ResolveVerticalIntervals(float lineLeft, float lineRight, float tolerance, bool includeAnyBlockOverlap = false)
    {
        var intervals = new List<SvgTextLineAreaFragment>();
        var bounds = EffectiveBounds;
        if (bounds.Width <= 0f ||
            bounds.Height <= 0f ||
            lineRight <= bounds.Left ||
            lineLeft >= bounds.Right)
        {
            return intervals;
        }

        if (Samples.Length < 2)
        {
            intervals.Add(new SvgTextLineAreaFragment(bounds.Top, bounds.Bottom));
            return intervals;
        }

        var sampleCoordinates = GetLineBandSampleCoordinates(lineLeft, lineRight, bounds.Left, bounds.Right);
        if (sampleCoordinates.Count == 0)
        {
            return intervals;
        }

        intervals = ResolveVerticalIntervalsAt(sampleCoordinates[0], bounds, tolerance);
        for (var i = 1; i < sampleCoordinates.Count; i++)
        {
            var sampleIntervals = ResolveVerticalIntervalsAt(sampleCoordinates[i], bounds, tolerance);
            if (includeAnyBlockOverlap)
            {
                intervals.AddRange(sampleIntervals);
                continue;
            }

            IntersectIntervals(intervals, sampleIntervals, tolerance);
            if (intervals.Count == 0)
            {
                return intervals;
            }
        }

        NormalizeIntervals(intervals);
        return intervals;
    }

    private List<SvgTextLineAreaFragment> ResolveVerticalIntervalsAt(float lineCenterX, SKRect bounds, float tolerance)
    {
        var intervals = new List<SvgTextLineAreaFragment>();
        var intersections = new List<(float Coordinate, int WindingDelta)>();
        for (var i = 1; i < Samples.Length; i++)
        {
            if (Samples[i].StartsSubpath)
            {
                continue;
            }

            var start = Samples[i - 1].Point;
            var end = Samples[i].Point;
            if (Math.Abs(end.X - start.X) <= 0.0001f)
            {
                continue;
            }

            if (!((start.X <= lineCenterX && end.X > lineCenterX) ||
                  (end.X <= lineCenterX && start.X > lineCenterX)))
            {
                continue;
            }

            var t = (lineCenterX - start.X) / (end.X - start.X);
            intersections.Add((start.Y + ((end.Y - start.Y) * t), end.X > start.X ? 1 : -1));
        }

        intersections.Sort(static (top, bottom) =>
        {
            var coordinateComparison = top.Coordinate.CompareTo(bottom.Coordinate);
            return coordinateComparison != 0
                ? coordinateComparison
                : top.WindingDelta.CompareTo(bottom.WindingDelta);
        });

        var edgeInset = Padding - Margin;
        if (FillType == SKPathFillType.EvenOdd)
        {
            for (var i = 0; i + 1 < intersections.Count; i += 2)
            {
                AddInterval(intersections[i].Coordinate, intersections[i + 1].Coordinate);
            }

            return intervals;
        }

        var winding = 0;
        float? startY = null;
        for (var i = 0; i < intersections.Count;)
        {
            var coordinate = intersections[i].Coordinate;
            var wasInside = winding != 0;
            while (i < intersections.Count &&
                   Math.Abs(intersections[i].Coordinate - coordinate) <= 0.0001f)
            {
                winding += intersections[i].WindingDelta;
                i++;
            }

            var isInside = winding != 0;
            if (!wasInside && isInside)
            {
                startY = coordinate;
            }
            else if (wasInside && !isInside && startY.HasValue)
            {
                AddInterval(startY.Value, coordinate);
                startY = null;
            }
        }

        return intervals;

        void AddInterval(float rawTop, float rawBottom)
        {
            var top = Math.Max(bounds.Top, Math.Min(rawTop, rawBottom) + edgeInset);
            var bottom = Math.Min(bounds.Bottom, Math.Max(rawTop, rawBottom) - edgeInset);
            if (bottom > top + tolerance)
            {
                intervals.Add(new SvgTextLineAreaFragment(top, bottom));
            }
        }
    }

    private static List<float> GetLineBandSampleCoordinates(float lineStart, float lineEnd, float boundsStart, float boundsEnd)
    {
        var samples = new List<float>(3);
        var overlapStart = Math.Max(lineStart, boundsStart);
        var overlapEnd = Math.Min(lineEnd, boundsEnd);
        if (overlapEnd < overlapStart)
        {
            return samples;
        }

        var overlapSize = overlapEnd - overlapStart;
        if (overlapSize <= 0.0001f)
        {
            samples.Add(Math.Max(boundsStart, Math.Min(boundsEnd, (overlapStart + overlapEnd) * 0.5f)));
            return samples;
        }

        var inset = Math.Min(0.01f, overlapSize * 0.25f);
        AddUnique(overlapStart + inset);
        AddUnique((overlapStart + overlapEnd) * 0.5f);
        AddUnique(overlapEnd - inset);
        return samples;

        void AddUnique(float coordinate)
        {
            coordinate = Math.Max(boundsStart, Math.Min(boundsEnd, coordinate));
            for (var i = 0; i < samples.Count; i++)
            {
                if (Math.Abs(samples[i] - coordinate) <= 0.0001f)
                {
                    return;
                }
            }

            samples.Add(coordinate);
        }
    }

    private static void IntersectIntervals(List<SvgTextLineAreaFragment> intervals, IReadOnlyList<SvgTextLineAreaFragment> other, float tolerance)
    {
        if (intervals.Count == 0 || other.Count == 0)
        {
            intervals.Clear();
            return;
        }

        var intersections = new List<SvgTextLineAreaFragment>();
        for (var i = 0; i < intervals.Count; i++)
        {
            for (var j = 0; j < other.Count; j++)
            {
                var start = Math.Max(intervals[i].Start, other[j].Start);
                var end = Math.Min(intervals[i].End, other[j].End);
                if (end > start + tolerance)
                {
                    intersections.Add(new SvgTextLineAreaFragment(start, end));
                }
            }
        }

        intervals.Clear();
        intervals.AddRange(intersections);
    }

    private static void NormalizeIntervals(List<SvgTextLineAreaFragment> intervals)
    {
        if (intervals.Count <= 1)
        {
            return;
        }

        intervals.Sort(static (left, right) =>
        {
            var startComparison = left.Start.CompareTo(right.Start);
            return startComparison != 0 ? startComparison : left.End.CompareTo(right.End);
        });

        var writeIndex = 0;
        for (var readIndex = 1; readIndex < intervals.Count; readIndex++)
        {
            var current = intervals[readIndex];
            var previous = intervals[writeIndex];
            if (current.Start <= previous.End + 0.0001f)
            {
                intervals[writeIndex] = new SvgTextLineAreaFragment(previous.Start, Math.Max(previous.End, current.End));
            }
            else
            {
                writeIndex++;
                intervals[writeIndex] = current;
            }
        }

        if (writeIndex + 1 < intervals.Count)
        {
            intervals.RemoveRange(writeIndex + 1, intervals.Count - writeIndex - 1);
        }
    }
}

internal sealed class SvgTextLineAreaProvider
{
    private const float DefaultTolerance = 1.5f;
    private const float IntervalMergeEpsilon = 0.0001f;
    private const int MaxLineSearchCount = 4096;

    private readonly SKRect _bounds;
    private readonly SvgTextLineShape[] _insideShapes;
    private readonly SvgTextLineShape[] _subtractShapes;
    private readonly bool _isShapeInside;
    private readonly bool _isVertical;
    private readonly SvgTextLineAreaFlow _flow;
    private readonly bool _isInlineDirectionReversed;
    private readonly float _shapeFirstBaselineOffset;
    private readonly float _tolerance;

    public SvgTextLineAreaProvider(
        SKRect bounds,
        IReadOnlyList<SvgTextLineShape> insideShapes,
        IReadOnlyList<SvgTextLineShape> subtractShapes,
        bool isShapeInside,
        bool isVertical,
        SvgTextLineAreaFlow flow,
        bool isInlineDirectionReversed,
        float shapeFirstBaselineOffset,
        float tolerance = DefaultTolerance)
    {
        _bounds = bounds;
        _insideShapes = CopyShapes(insideShapes);
        _subtractShapes = CopyShapes(subtractShapes);
        _isShapeInside = isShapeInside;
        _isVertical = isVertical;
        _flow = flow;
        _isInlineDirectionReversed = isInlineDirectionReversed;
        _shapeFirstBaselineOffset = shapeFirstBaselineOffset;
        _tolerance = tolerance;
    }

    public SvgTextLineArea ResolveLineArea(float blockCoordinate, float lineAdvance)
    {
        return _isVertical
            ? ResolveVerticalLineArea(blockCoordinate, lineAdvance)
            : ResolveHorizontalLineArea(blockCoordinate, lineAdvance);
    }

    public SvgTextLineArea ResolveWrappedLineArea(int lineIndex, float firstBlockCoordinate, float lineAdvance, int blockProgression)
    {
        if (!_isShapeInside || _insideShapes.Length == 0)
        {
            return ResolveLineArea(firstBlockCoordinate + (lineIndex * lineAdvance * blockProgression), lineAdvance);
        }

        if (lineAdvance <= 0f || lineIndex < 0)
        {
            return CreateOutOfRangeLineArea(firstBlockCoordinate);
        }

        for (var i = 0; i < _insideShapes.Length; i++)
        {
            var shape = _insideShapes[i];
            if (_isVertical)
            {
                var shapeBounds = shape.EffectiveBounds;
                var shapeFirstBaselineX = GetVerticalFirstBaselineX(shapeBounds, lineAdvance, blockProgression, _shapeFirstBaselineOffset);
                var shapeLineCount = GetVerticalMaxWrappedLineSearchCount(shapeBounds, shapeFirstBaselineX, lineAdvance, blockProgression);
                if (lineIndex < shapeLineCount)
                {
                    return ResolveVerticalLineArea(
                        shapeBounds,
                        shape,
                        shapeFirstBaselineX + (lineIndex * lineAdvance * blockProgression),
                        lineAdvance,
                        i);
                }

                lineIndex -= shapeLineCount;
            }
            else
            {
                var shapeBounds = shape.EffectiveBounds;
                var shapeFirstBaselineY = shapeBounds.Top + _shapeFirstBaselineOffset;
                var shapeLineCount = GetHorizontalMaxWrappedLineSearchCount(shapeBounds, shapeFirstBaselineY, lineAdvance);
                if (lineIndex < shapeLineCount)
                {
                    return ResolveHorizontalLineArea(
                        shapeBounds,
                        shape,
                        shapeFirstBaselineY + (lineIndex * lineAdvance),
                        lineAdvance,
                        i);
                }

                lineIndex -= shapeLineCount;
            }
        }

        return CreateOutOfRangeLineArea(firstBlockCoordinate);
    }

    public int GetMaxWrappedLineSearchCount(float firstBlockCoordinate, float lineAdvance, int segmentCount, int blockProgression = 1)
    {
        if (lineAdvance <= 0f)
        {
            return 0;
        }

        if (_isShapeInside && _insideShapes.Length > 0)
        {
            var totalLineCount = 0;
            for (var i = 0; i < _insideShapes.Length; i++)
            {
                var shapeBounds = _insideShapes[i].EffectiveBounds;
                totalLineCount += _isVertical
                    ? GetVerticalMaxWrappedLineSearchCount(
                        shapeBounds,
                        GetVerticalFirstBaselineX(shapeBounds, lineAdvance, blockProgression, _shapeFirstBaselineOffset),
                        lineAdvance,
                        blockProgression)
                    : GetHorizontalMaxWrappedLineSearchCount(
                        shapeBounds,
                        shapeBounds.Top + _shapeFirstBaselineOffset,
                        lineAdvance);
            }

            return Math.Max(0, Math.Min(MaxLineSearchCount, totalLineCount));
        }

        if (_isVertical)
        {
            if (!HasFiniteHorizontalBounds(_bounds))
            {
                return GetUnboundedVerticalMaxWrappedLineSearchCount(
                    firstBlockCoordinate,
                    lineAdvance,
                    segmentCount,
                    blockProgression);
            }

            var maxSearchCount = Math.Min(MaxLineSearchCount, Math.Max(1, segmentCount + 1));
            var count = 0;
            for (var i = 0; i < maxSearchCount; i++)
            {
                var baselineX = firstBlockCoordinate + (i * lineAdvance * blockProgression);
                GetVerticalLineExtents(baselineX, lineAdvance, blockProgression, out var lineLeft, out var lineRight);
                if (lineRight < _bounds.Left - _tolerance ||
                    lineLeft > _bounds.Right + _tolerance)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        if (!HasFiniteVerticalBounds(_bounds))
        {
            return GetUnboundedHorizontalMaxWrappedLineSearchCount(
                firstBlockCoordinate,
                lineAdvance,
                segmentCount,
                blockProgression);
        }

        return GetHorizontalMaxWrappedLineSearchCount(_bounds, firstBlockCoordinate, lineAdvance);
    }

    private int GetUnboundedHorizontalMaxWrappedLineSearchCount(
        float firstBaselineY,
        float lineAdvance,
        int segmentCount,
        int blockProgression)
    {
        var baseCount = Math.Min(MaxLineSearchCount, Math.Max(1, segmentCount + 1));
        if (_subtractShapes.Length == 0 || !HasFiniteHorizontalBounds(_bounds))
        {
            return baseCount;
        }

        var requiredCount = baseCount;
        var step = Math.Abs(lineAdvance * blockProgression);
        if (step <= 0f)
        {
            return requiredCount;
        }

        for (var i = 0; i < _subtractShapes.Length; i++)
        {
            var shapeBounds = _subtractShapes[i].EffectiveBounds;
            if (shapeBounds.IsEmpty ||
                !RangesOverlap(shapeBounds.Left, shapeBounds.Right, _bounds.Left, _bounds.Right))
            {
                continue;
            }

            var targetBaseline = blockProgression < 0
                ? shapeBounds.Top - (lineAdvance * 0.25f)
                : shapeBounds.Bottom + (lineAdvance * 0.85f);
            var distance = blockProgression < 0
                ? firstBaselineY - targetBaseline
                : targetBaseline - firstBaselineY;
            if (distance <= 0f)
            {
                continue;
            }

            var clearLineIndex = (int)Math.Ceiling(distance / step);
            requiredCount = Math.Max(requiredCount, clearLineIndex + baseCount);
        }

        return Math.Min(MaxLineSearchCount, requiredCount);
    }

    private int GetUnboundedVerticalMaxWrappedLineSearchCount(
        float firstBaselineX,
        float lineAdvance,
        int segmentCount,
        int blockProgression)
    {
        var baseCount = Math.Min(MaxLineSearchCount, Math.Max(1, segmentCount + 1));
        if (_subtractShapes.Length == 0 || !HasFiniteVerticalBounds(_bounds))
        {
            return baseCount;
        }

        var requiredCount = baseCount;
        var step = Math.Abs(lineAdvance * blockProgression);
        if (step <= 0f)
        {
            return requiredCount;
        }

        for (var i = 0; i < _subtractShapes.Length; i++)
        {
            var shapeBounds = _subtractShapes[i].EffectiveBounds;
            if (shapeBounds.IsEmpty ||
                !RangesOverlap(shapeBounds.Top, shapeBounds.Bottom, _bounds.Top, _bounds.Bottom))
            {
                continue;
            }

            var targetBaseline = blockProgression < 0
                ? shapeBounds.Left - (lineAdvance * 0.85f)
                : shapeBounds.Right + (lineAdvance * 0.85f);
            var distance = blockProgression < 0
                ? firstBaselineX - targetBaseline
                : targetBaseline - firstBaselineX;
            if (distance <= 0f)
            {
                continue;
            }

            var clearLineIndex = (int)Math.Ceiling(distance / step);
            requiredCount = Math.Max(requiredCount, clearLineIndex + baseCount);
        }

        return Math.Min(MaxLineSearchCount, requiredCount);
    }

    private static float GetVerticalFirstBaselineX(SKRect bounds, float lineAdvance, int blockProgression, float firstBaselineOffset)
    {
        firstBaselineOffset = firstBaselineOffset > 0f
            ? firstBaselineOffset
            : lineAdvance * 0.85f;

        var baseline = blockProgression < 0
            ? bounds.Right - firstBaselineOffset
            : bounds.Left + firstBaselineOffset;
        if (!HasFiniteHorizontalBounds(bounds) || bounds.Width <= 0f || lineAdvance <= 0f)
        {
            return baseline;
        }

        if (blockProgression < 0)
        {
            return Math.Max(bounds.Left + (lineAdvance * 0.25f), baseline);
        }

        return Math.Min(bounds.Right - (lineAdvance * 0.25f), baseline);
    }

    private SvgTextLineArea ResolveHorizontalLineArea(float baselineY, float lineAdvance)
    {
        var insideShape = _insideShapes.Length > 0 ? _insideShapes[0] : (SvgTextLineShape?)null;
        var bounds = insideShape.HasValue ? insideShape.Value.EffectiveBounds : _bounds;
        return ResolveHorizontalLineArea(bounds, insideShape, baselineY, lineAdvance, insideShape.HasValue ? 0 : -1);
    }

    private SvgTextLineArea ResolveHorizontalLineArea(
        SKRect bounds,
        SvgTextLineShape? insideShape,
        float baselineY,
        float lineAdvance,
        int shapeIndex)
    {
        if (lineAdvance <= 0f)
        {
            return CreateEmptyLineArea(bounds.Left, baselineY, shapeIndex);
        }

        var intervals = ResolveHorizontalLineAreaFragments(bounds, insideShape, baselineY, lineAdvance);
        return CreateLineAreaFromIntervals(bounds.Left, baselineY, intervals, PreferLaterHorizontalFragment(), shapeIndex);
    }

    private List<SvgTextLineAreaFragment> ResolveHorizontalLineAreaFragments(
        SKRect bounds,
        SvgTextLineShape? insideShape,
        float baselineY,
        float lineAdvance)
    {
        var lineTop = baselineY - (lineAdvance * 0.85f);
        var lineBottom = baselineY + (lineAdvance * 0.25f);
        if (HasFiniteVerticalBounds(bounds) &&
            (lineTop < bounds.Top - _tolerance || lineBottom > bounds.Bottom + _tolerance))
        {
            return new List<SvgTextLineAreaFragment>();
        }

        var intervals = insideShape.HasValue
            ? insideShape.Value.ResolveHorizontalIntervals(lineTop, lineBottom, _tolerance)
            : new List<SvgTextLineAreaFragment> { new(bounds.Left, bounds.Right) };

        if (intervals.Count == 0)
        {
            return intervals;
        }

        for (var subtractIndex = 0; subtractIndex < _subtractShapes.Length; subtractIndex++)
        {
            var subtractIntervals = _subtractShapes[subtractIndex].ResolveHorizontalIntervals(lineTop, lineBottom, _tolerance, includeAnyBlockOverlap: true);
            for (var subtractIntervalIndex = 0; subtractIntervalIndex < subtractIntervals.Count; subtractIntervalIndex++)
            {
                var subtractLeft = Math.Max(bounds.Left, subtractIntervals[subtractIntervalIndex].Start);
                var subtractRight = Math.Min(bounds.Right, subtractIntervals[subtractIntervalIndex].End);
                SubtractInterval(intervals, subtractLeft, subtractRight);
            }
        }

        NormalizeIntervals(intervals);
        return intervals;
    }

    private SvgTextLineArea ResolveVerticalLineArea(float baselineX, float lineAdvance)
    {
        var insideShape = _insideShapes.Length > 0 ? _insideShapes[0] : (SvgTextLineShape?)null;
        var bounds = insideShape.HasValue ? insideShape.Value.EffectiveBounds : _bounds;
        return ResolveVerticalLineArea(bounds, insideShape, baselineX, lineAdvance, insideShape.HasValue ? 0 : -1);
    }

    private SvgTextLineArea ResolveVerticalLineArea(
        SKRect bounds,
        SvgTextLineShape? insideShape,
        float baselineX,
        float lineAdvance,
        int shapeIndex)
    {
        if (lineAdvance <= 0f)
        {
            return CreateEmptyLineArea(bounds.Top, baselineX, shapeIndex);
        }

        var intervals = ResolveVerticalLineAreaFragments(bounds, insideShape, baselineX, lineAdvance);
        return CreateLineAreaFromIntervals(bounds.Top, baselineX, intervals, _isInlineDirectionReversed, shapeIndex);
    }

    private List<SvgTextLineAreaFragment> ResolveVerticalLineAreaFragments(
        SKRect bounds,
        SvgTextLineShape? insideShape,
        float baselineX,
        float lineAdvance)
    {
        GetVerticalLineExtents(baselineX, lineAdvance, out var lineLeft, out var lineRight);
        if (HasFiniteHorizontalBounds(bounds) &&
            (lineRight <= bounds.Left + _tolerance || lineLeft >= bounds.Right - _tolerance))
        {
            return new List<SvgTextLineAreaFragment>();
        }

        var intervals = insideShape.HasValue
            ? insideShape.Value.ResolveVerticalIntervals(lineLeft, lineRight, _tolerance)
            : new List<SvgTextLineAreaFragment> { new(bounds.Top, bounds.Bottom) };

        if (intervals.Count == 0)
        {
            return intervals;
        }

        for (var subtractIndex = 0; subtractIndex < _subtractShapes.Length; subtractIndex++)
        {
            var subtractIntervals = _subtractShapes[subtractIndex].ResolveVerticalIntervals(lineLeft, lineRight, _tolerance, includeAnyBlockOverlap: true);
            for (var subtractIntervalIndex = 0; subtractIntervalIndex < subtractIntervals.Count; subtractIntervalIndex++)
            {
                var subtractTop = Math.Max(bounds.Top, subtractIntervals[subtractIntervalIndex].Start);
                var subtractBottom = Math.Min(bounds.Bottom, subtractIntervals[subtractIntervalIndex].End);
                SubtractInterval(intervals, subtractTop, subtractBottom);
            }
        }

        NormalizeIntervals(intervals);
        return intervals;
    }

    private SvgTextLineArea CreateLineAreaFromIntervals(
        float fallbackStart,
        float blockCoordinate,
        IReadOnlyList<SvgTextLineAreaFragment> intervals,
        bool preferLaterFragment,
        int shapeIndex)
    {
        if (intervals.Count == 0)
        {
            return CreateEmptyLineArea(fallbackStart, blockCoordinate, shapeIndex);
        }

        var bestStart = fallbackStart;
        var bestSize = 0f;
        for (var i = 0; i < intervals.Count; i++)
        {
            var size = intervals[i].InlineSize;
            if (size > bestSize ||
                (preferLaterFragment && Math.Abs(size - bestSize) <= _tolerance && intervals[i].Start > bestStart))
            {
                bestStart = intervals[i].Start;
                bestSize = size;
            }
        }

        return bestSize > 0f
            ? new SvgTextLineArea(bestStart, bestSize, blockCoordinate, CopyIntervalsForFlow(intervals, preferLaterFragment), shapeIndex, GetInlineProgression())
            : CreateEmptyLineArea(fallbackStart, blockCoordinate, shapeIndex);
    }

    private SvgTextLineArea CreateEmptyLineArea(float start, float blockCoordinate, int shapeIndex)
    {
        return new SvgTextLineArea(start, 0f, blockCoordinate, Array.Empty<SvgTextLineAreaFragment>(), shapeIndex, GetInlineProgression());
    }

    private SvgTextLineArea CreateOutOfRangeLineArea(float blockCoordinate)
    {
        return new SvgTextLineArea(_isVertical ? _bounds.Top : _bounds.Left, 0f, blockCoordinate, Array.Empty<SvgTextLineAreaFragment>(), -1, GetInlineProgression());
    }

    private bool PreferLaterHorizontalFragment()
    {
        return _flow == SvgTextLineAreaFlow.HorizontalRightToLeft;
    }

    private int GetInlineProgression()
    {
        return _isInlineDirectionReversed ? -1 : 1;
    }

    private void SubtractInterval(List<SvgTextLineAreaFragment> intervals, float subtractStart, float subtractEnd)
    {
        if (subtractEnd <= subtractStart)
        {
            return;
        }

        for (var intervalIndex = intervals.Count - 1; intervalIndex >= 0; intervalIndex--)
        {
            var interval = intervals[intervalIndex];
            if (subtractEnd <= interval.Start || subtractStart >= interval.End)
            {
                continue;
            }

            intervals.RemoveAt(intervalIndex);
            if (subtractStart > interval.Start + _tolerance)
            {
                intervals.Add(new SvgTextLineAreaFragment(interval.Start, subtractStart));
            }

            if (subtractEnd < interval.End - _tolerance)
            {
                intervals.Add(new SvgTextLineAreaFragment(subtractEnd, interval.End));
            }
        }
    }

    private void NormalizeIntervals(List<SvgTextLineAreaFragment> intervals)
    {
        if (intervals.Count <= 1)
        {
            return;
        }

        intervals.Sort(static (left, right) =>
        {
            var startComparison = left.Start.CompareTo(right.Start);
            return startComparison != 0 ? startComparison : left.End.CompareTo(right.End);
        });

        var writeIndex = 0;
        for (var readIndex = 1; readIndex < intervals.Count; readIndex++)
        {
            var current = intervals[readIndex];
            var previous = intervals[writeIndex];
            if (current.Start <= previous.End + IntervalMergeEpsilon)
            {
                intervals[writeIndex] = new SvgTextLineAreaFragment(previous.Start, Math.Max(previous.End, current.End));
            }
            else
            {
                writeIndex++;
                intervals[writeIndex] = current;
            }
        }

        if (writeIndex + 1 < intervals.Count)
        {
            intervals.RemoveRange(writeIndex + 1, intervals.Count - writeIndex - 1);
        }
    }

    private int GetHorizontalMaxWrappedLineSearchCount(SKRect bounds, float firstBaselineY, float lineAdvance)
    {
        if (!HasFiniteVerticalBounds(bounds))
        {
            return MaxLineSearchCount;
        }

        var lastBaselineY = bounds.Bottom - (lineAdvance * 0.25f);
        if (firstBaselineY > lastBaselineY + _tolerance)
        {
            return 0;
        }

        var horizontalCount = (int)Math.Floor((lastBaselineY - firstBaselineY) / lineAdvance) + 1;
        return Math.Max(0, Math.Min(MaxLineSearchCount, horizontalCount));
    }

    private int GetVerticalMaxWrappedLineSearchCount(SKRect bounds, float firstBaselineX, float lineAdvance, int blockProgression)
    {
        if (!HasFiniteHorizontalBounds(bounds))
        {
            return MaxLineSearchCount;
        }

        if (blockProgression < 0)
        {
            var lastBaselineX = bounds.Left + (lineAdvance * 0.25f);
            if (firstBaselineX < lastBaselineX - _tolerance)
            {
                return 0;
            }

            var verticalCount = (int)Math.Floor((firstBaselineX - lastBaselineX) / lineAdvance) + 1;
            return Math.Max(0, Math.Min(MaxLineSearchCount, verticalCount));
        }

        var lastForwardBaselineX = bounds.Right - (lineAdvance * 0.25f);
        if (firstBaselineX > lastForwardBaselineX + _tolerance)
        {
            return 0;
        }

        var forwardCount = (int)Math.Floor((lastForwardBaselineX - firstBaselineX) / lineAdvance) + 1;
        return Math.Max(0, Math.Min(MaxLineSearchCount, forwardCount));
    }

    private void GetVerticalLineExtents(float baselineX, float lineAdvance, out float lineLeft, out float lineRight)
    {
        GetVerticalLineExtents(
            baselineX,
            lineAdvance,
            _flow == SvgTextLineAreaFlow.VerticalRightToLeftColumns ? -1 : 1,
            out lineLeft,
            out lineRight);
    }

    private static void GetVerticalLineExtents(float baselineX, float lineAdvance, int blockProgression, out float lineLeft, out float lineRight)
    {
        if (blockProgression < 0)
        {
            lineLeft = baselineX - (lineAdvance * 0.25f);
            lineRight = baselineX + (lineAdvance * 0.85f);
            return;
        }

        lineLeft = baselineX - (lineAdvance * 0.85f);
        lineRight = baselineX + (lineAdvance * 0.25f);
    }

    private static bool HasFiniteHorizontalBounds(SKRect bounds)
    {
        return !float.IsNegativeInfinity(bounds.Left) && !float.IsPositiveInfinity(bounds.Right);
    }

    private static bool HasFiniteVerticalBounds(SKRect bounds)
    {
        return !float.IsNegativeInfinity(bounds.Top) && !float.IsPositiveInfinity(bounds.Bottom);
    }

    private static bool RangesOverlap(float firstStart, float firstEnd, float secondStart, float secondEnd)
    {
        return firstEnd > secondStart && firstStart < secondEnd;
    }

    private static SvgTextLineShape[] CopyShapes(IReadOnlyList<SvgTextLineShape> shapes)
    {
        if (shapes.Count == 0)
        {
            return Array.Empty<SvgTextLineShape>();
        }

        var copy = new SvgTextLineShape[shapes.Count];
        for (var i = 0; i < shapes.Count; i++)
        {
            copy[i] = shapes[i];
        }

        return copy;
    }

    private static SvgTextLineAreaFragment[] CopyIntervals(IReadOnlyList<SvgTextLineAreaFragment> intervals)
    {
        if (intervals.Count == 0)
        {
            return Array.Empty<SvgTextLineAreaFragment>();
        }

        var copy = new SvgTextLineAreaFragment[intervals.Count];
        for (var i = 0; i < intervals.Count; i++)
        {
            copy[i] = intervals[i];
        }

        return copy;
    }

    private static SvgTextLineAreaFragment[] CopyIntervalsForFlow(
        IReadOnlyList<SvgTextLineAreaFragment> intervals,
        bool reverseInlineOrder)
    {
        if (intervals.Count == 0)
        {
            return Array.Empty<SvgTextLineAreaFragment>();
        }

        var copy = CopyIntervals(intervals);
        if (!reverseInlineOrder || copy.Length <= 1)
        {
            return copy;
        }

        Array.Reverse(copy);
        return copy;
    }
}
