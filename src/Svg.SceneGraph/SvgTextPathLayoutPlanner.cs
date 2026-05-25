using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShimSkiaSharp;

namespace Svg.Skia;

internal static class SvgTextPathLayoutPlanner
{
    private const float TangentEpsilon = 1e-12f;

    internal readonly record struct PathSample(SKPoint Point, float Distance, bool StartsSubpath, bool ClosesSubpath);

    internal readonly record struct MappingOptions(float PathLength, bool IsClosedLoop, float StartOffset, float BaseVOffset);

    internal readonly record struct LinePlacementInput(float InlineOffset, float BaseVOffset, float Advance);

    internal readonly record struct LinePlacement(
        float InlineOffset,
        float BaseVOffset,
        float Advance,
        SKPoint Point,
        SKPoint Tangent,
        float RotationDegrees);

    internal readonly record struct ClusterPlacementInput(
        string Text,
        float Advance,
        float SpacingAfter,
        float RotationDegrees,
        float ScaleX);

    internal readonly record struct ClusterPlacement(
        string Text,
        SKPoint Point,
        float RotationDegrees,
        float ScaleX,
        float ScaleOriginX,
        float InlineOffset,
        float SampleOffset,
        float Advance);

    internal sealed class TextPathPlacementPlan
    {
        public TextPathPlacementPlan(string text, ClusterPlacement[] placements)
        {
            Text = text;
            Placements = placements;
        }

        public string Text { get; }

        public IReadOnlyList<ClusterPlacement> Placements { get; }
    }

    internal sealed class TextPathLinePlacementPlan
    {
        public TextPathLinePlacementPlan(LinePlacement[] placements)
        {
            Placements = placements;
        }

        public IReadOnlyList<LinePlacement> Placements { get; }
    }

    internal readonly record struct StretchClusterInput(float NaturalOffset, float NaturalAdvance, float SpacingAfter);

    internal readonly record struct StretchClusterPlacement(int ClusterIndex, float AdjustedOffset, float SpacingOffset, float ExtraGapOffset, float ScaleX);

    internal readonly record struct StretchTextClusterRange(int Start, int End);

    internal sealed class StretchClusterPlan
    {
        public StretchClusterPlan(float adjustedAdvance, StretchClusterPlacement[] placements)
        {
            AdjustedAdvance = adjustedAdvance;
            Placements = placements;
        }

        public float AdjustedAdvance { get; }

        public IReadOnlyList<StretchClusterPlacement> Placements { get; }
    }

    internal static bool TryCreatePlacementPlan(
        IReadOnlyList<ClusterPlacementInput> clusters,
        IReadOnlyList<PathSample> pathSamples,
        MappingOptions options,
        out TextPathPlacementPlan plan)
    {
        plan = new TextPathPlacementPlan(string.Empty, Array.Empty<ClusterPlacement>());
        if (clusters.Count == 0 || pathSamples.Count < 2)
        {
            return false;
        }

        var pathLength = ResolvePathLength(pathSamples, options.PathLength);
        if (pathLength <= 0f && !options.IsClosedLoop)
        {
            return false;
        }

        var currentOffset = options.StartOffset;
        var closedLoopEndOffset = options.IsClosedLoop && pathLength > 0f
            ? options.StartOffset + pathLength
            : float.PositiveInfinity;
        var pathSegmentIndex = 1;
        var previousSampleOffset = float.NegativeInfinity;
        var visibleText = new StringBuilder();
        var placements = new List<ClusterPlacement>(clusters.Count);

        for (var i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            var advance = IsValidPositiveAdvance(cluster.Advance) ? cluster.Advance : 0f;
            var clusterAdvance = advance + cluster.SpacingAfter;
            var glyphMidOffset = currentOffset + (advance * 0.5f);
            if (glyphMidOffset >= closedLoopEndOffset)
            {
                break;
            }

            var sampleOffset = glyphMidOffset;
            if (options.IsClosedLoop && pathLength > 0f)
            {
                sampleOffset = NormalizeClosedPathDistance(glyphMidOffset, pathLength);
            }
            else if (glyphMidOffset <= 0f)
            {
                currentOffset += clusterAdvance;
                continue;
            }

            if (!options.IsClosedLoop && glyphMidOffset >= pathLength)
            {
                break;
            }

            if (sampleOffset < previousSampleOffset)
            {
                pathSegmentIndex = 1;
            }

            previousSampleOffset = sampleOffset;

            if (!TryGetPointAndTangent(pathSamples, sampleOffset, ref pathSegmentIndex, out var rawPoint, out var tangent))
            {
                return false;
            }

            var angleDegrees = (float)(Math.Atan2(tangent.Y, tangent.X) * 180d / Math.PI);
            var finalAngleDegrees = angleDegrees + cluster.RotationDegrees;
            var baselineDirection = RotateTangent(tangent, cluster.RotationDegrees);
            var baselineNormal = new SKPoint(-baselineDirection.Y, baselineDirection.X);
            var point = new SKPoint(
                rawPoint.X + (baselineNormal.X * options.BaseVOffset) - (baselineDirection.X * advance * 0.5f),
                rawPoint.Y + (baselineNormal.Y * options.BaseVOffset) - (baselineDirection.Y * advance * 0.5f));

            placements.Add(new ClusterPlacement(
                cluster.Text,
                point,
                finalAngleDegrees,
                cluster.ScaleX,
                point.X,
                currentOffset,
                sampleOffset,
                advance));
            visibleText.Append(cluster.Text);

            if (i < clusters.Count - 1)
            {
                currentOffset += clusterAdvance;
            }
        }

        if (placements.Count == 0)
        {
            return false;
        }

        plan = new TextPathPlacementPlan(visibleText.ToString(), placements.ToArray());
        return true;
    }

    internal static bool TryCreateLinePlacementPlan(
        IReadOnlyList<LinePlacementInput> lines,
        IReadOnlyList<PathSample> pathSamples,
        MappingOptions options,
        bool isVertical,
        float lineAdvance,
        out TextPathLinePlacementPlan plan)
    {
        plan = new TextPathLinePlacementPlan(Array.Empty<LinePlacement>());
        if (lines.Count == 0 || pathSamples.Count < 2)
        {
            return false;
        }

        var pathLength = ResolvePathLength(pathSamples, options.PathLength);
        if (pathLength <= 0f && !options.IsClosedLoop)
        {
            return false;
        }

        var placements = new List<LinePlacement>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var inlineOffset = options.StartOffset + line.InlineOffset;
            var baseVOffset = options.BaseVOffset + line.BaseVOffset;
            if (isVertical && lineAdvance != 0f)
            {
                baseVOffset += lineAdvance * i;
            }

            if (!TryGetCurrentPosition(pathSamples, inlineOffset, baseVOffset, options.IsClosedLoop, out var point, out var tangent))
            {
                return false;
            }

            placements.Add(new LinePlacement(
                inlineOffset,
                baseVOffset,
                line.Advance,
                point,
                tangent,
                (float)(Math.Atan2(tangent.Y, tangent.X) * 180d / Math.PI)));
        }

        if (placements.Count == 0)
        {
            return false;
        }

        plan = new TextPathLinePlacementPlan(placements.ToArray());
        return true;
    }

    internal static bool TryCreateStretchClusterPlan(
        IReadOnlyList<StretchClusterInput> clusters,
        float naturalAdvance,
        float targetAdvance,
        bool distributeTextLengthGap,
        bool scaleGlyphsAndSpacing,
        out StretchClusterPlan plan)
    {
        plan = new StretchClusterPlan(0f, Array.Empty<StretchClusterPlacement>());
        if (clusters.Count == 0 || naturalAdvance <= 0f)
        {
            return false;
        }

        var totalSpacingAdvance = 0f;
        for (var i = 0; i < clusters.Count - 1; i++)
        {
            totalSpacingAdvance += clusters[i].SpacingAfter;
        }

        var naturalClusterAdvance = naturalAdvance + totalSpacingAdvance;
        if (naturalClusterAdvance <= 0f)
        {
            return false;
        }

        var adjustedAdvance = targetAdvance > 0f ? targetAdvance : naturalClusterAdvance;
        var scaleX = scaleGlyphsAndSpacing && adjustedAdvance > 0f
            ? adjustedAdvance / naturalClusterAdvance
            : 1f;
        var extraGapAdvance = 0f;
        if (distributeTextLengthGap)
        {
            if (clusters.Count <= 1 || targetAdvance <= 0f)
            {
                return false;
            }

            extraGapAdvance = (targetAdvance - naturalClusterAdvance) / (clusters.Count - 1);
        }

        var spacingOffset = 0f;
        var placements = new StretchClusterPlacement[clusters.Count];
        for (var i = 0; i < clusters.Count; i++)
        {
            placements[i] = new StretchClusterPlacement(
                i,
                (clusters[i].NaturalOffset + spacingOffset + (extraGapAdvance * i)) * scaleX,
                spacingOffset,
                extraGapAdvance * i,
                scaleX);

            if (i < clusters.Count - 1)
            {
                spacingOffset += clusters[i].SpacingAfter;
            }
        }

        plan = new StretchClusterPlan(adjustedAdvance, placements);
        return true;
    }

    internal static bool TryCreateStretchTextClusterRanges(
        string text,
        IReadOnlyList<int> shapedClusters,
        out StretchTextClusterRange[] ranges)
    {
        ranges = Array.Empty<StretchTextClusterRange>();
        if (string.IsNullOrEmpty(text) || shapedClusters.Count == 0)
        {
            return false;
        }

        var textElementRanges = CreateTextElementRanges(text);
        if (textElementRanges.Count == 0)
        {
            return false;
        }

        var starts = new SortedSet<int>();
        for (var i = 0; i < shapedClusters.Count; i++)
        {
            var clusterStart = shapedClusters[i];
            if (clusterStart < 0 || clusterStart >= text.Length)
            {
                return false;
            }

            if (!TryFindTextElementRange(textElementRanges, clusterStart, out var textElementRange))
            {
                return false;
            }

            starts.Add(textElementRange.Start);
        }

        if (starts.Count == 0)
        {
            return false;
        }

        var orderedStarts = new int[starts.Count];
        starts.CopyTo(orderedStarts);

        ranges = new StretchTextClusterRange[orderedStarts.Length];
        for (var i = 0; i < orderedStarts.Length; i++)
        {
            var start = orderedStarts[i];
            var end = i + 1 < orderedStarts.Length ? orderedStarts[i + 1] : text.Length;
            if (end <= start)
            {
                ranges = Array.Empty<StretchTextClusterRange>();
                return false;
            }

            ranges[i] = new StretchTextClusterRange(start, end);
        }

        return true;
    }

    internal static bool TryCreateFallbackStretchTextClusterRanges(
        string text,
        out StretchTextClusterRange[] ranges)
    {
        ranges = Array.Empty<StretchTextClusterRange>();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var textElementRanges = CreateTextElementRanges(text);
        if (textElementRanges.Count == 0)
        {
            return false;
        }

        ranges = textElementRanges.ToArray();
        return true;
    }

    internal static bool TryWarpTextOutlinePath(
        SKPath glyphPath,
        IReadOnlyList<PathSample> pathSamples,
        MappingOptions options,
        out SKPath stretchedPath)
    {
        stretchedPath = new SKPath { FillType = glyphPath.FillType };
        if (glyphPath.Commands is null || glyphPath.Commands.Count == 0 || pathSamples.Count < 2)
        {
            return false;
        }

        var appendedCommand = false;
        for (var i = 0; i < glyphPath.Commands.Count; i++)
        {
            switch (glyphPath.Commands[i])
            {
                case MoveToPathCommand moveTo:
                    if (!TryMapTextPoint(new SKPoint(moveTo.X, moveTo.Y), pathSamples, options, out var movePoint))
                    {
                        return false;
                    }

                    stretchedPath.MoveTo(movePoint.X, movePoint.Y);
                    appendedCommand = true;
                    break;

                case LineToPathCommand lineTo:
                    if (!TryMapTextPoint(new SKPoint(lineTo.X, lineTo.Y), pathSamples, options, out var linePoint))
                    {
                        return false;
                    }

                    stretchedPath.LineTo(linePoint.X, linePoint.Y);
                    appendedCommand = true;
                    break;

                case QuadToPathCommand quadTo:
                    if (!TryMapTextPoint(new SKPoint(quadTo.X0, quadTo.Y0), pathSamples, options, out var quadControl) ||
                        !TryMapTextPoint(new SKPoint(quadTo.X1, quadTo.Y1), pathSamples, options, out var quadEnd))
                    {
                        return false;
                    }

                    stretchedPath.QuadTo(quadControl.X, quadControl.Y, quadEnd.X, quadEnd.Y);
                    appendedCommand = true;
                    break;

                case CubicToPathCommand cubicTo:
                    if (!TryMapTextPoint(new SKPoint(cubicTo.X0, cubicTo.Y0), pathSamples, options, out var cubicControl1) ||
                        !TryMapTextPoint(new SKPoint(cubicTo.X1, cubicTo.Y1), pathSamples, options, out var cubicControl2) ||
                        !TryMapTextPoint(new SKPoint(cubicTo.X2, cubicTo.Y2), pathSamples, options, out var cubicEnd))
                    {
                        return false;
                    }

                    stretchedPath.CubicTo(cubicControl1.X, cubicControl1.Y, cubicControl2.X, cubicControl2.Y, cubicEnd.X, cubicEnd.Y);
                    appendedCommand = true;
                    break;

                case AddRectPathCommand addRect:
                    if (!TryAppendStretchedRect(addRect.Rect, pathSamples, options, stretchedPath))
                    {
                        return false;
                    }

                    appendedCommand = true;
                    break;

                case AddRoundRectPathCommand addRoundRect:
                    if (!TryAppendStretchedRect(addRoundRect.Rect, pathSamples, options, stretchedPath))
                    {
                        return false;
                    }

                    appendedCommand = true;
                    break;

                case AddOvalPathCommand addOval:
                    if (!TryAppendStretchedEllipse(addOval.Rect, pathSamples, options, stretchedPath))
                    {
                        return false;
                    }

                    appendedCommand = true;
                    break;

                case AddCirclePathCommand addCircle:
                    if (!TryAppendStretchedEllipse(
                            SKRect.Create(
                                addCircle.X - addCircle.Radius,
                                addCircle.Y - addCircle.Radius,
                                addCircle.Radius * 2f,
                                addCircle.Radius * 2f),
                            pathSamples,
                            options,
                            stretchedPath))
                    {
                        return false;
                    }

                    appendedCommand = true;
                    break;

                case AddPolyPathCommand addPoly:
                    if (!TryAppendStretchedPoly(addPoly.Points, addPoly.Close, pathSamples, options, stretchedPath))
                    {
                        return false;
                    }

                    appendedCommand = true;
                    break;

                case ClosePathCommand:
                    stretchedPath.Close();
                    break;

                default:
                    return false;
            }
        }

        return appendedCommand && !stretchedPath.IsEmpty;
    }

    internal static bool TryWarpTextOutlinePathOrFallback(
        SKPath glyphPath,
        SKRect fallbackBounds,
        IReadOnlyList<PathSample> pathSamples,
        MappingOptions options,
        out SKPath stretchedPath)
    {
        if (TryWarpTextOutlinePath(glyphPath, pathSamples, options, out stretchedPath))
        {
            return true;
        }

        stretchedPath = new SKPath { FillType = glyphPath.FillType };
        if (!IsRenderableRect(fallbackBounds) ||
            !TryAppendStretchedRect(fallbackBounds, pathSamples, options, stretchedPath))
        {
            stretchedPath = new SKPath();
            return false;
        }

        return !stretchedPath.IsEmpty;
    }

    internal static bool TryMapTextPoint(
        SKPoint textPoint,
        IReadOnlyList<PathSample> pathSamples,
        MappingOptions options,
        out SKPoint mappedPoint)
    {
        mappedPoint = default;
        var pathLength = ResolvePathLength(pathSamples, options.PathLength);
        var distance = options.StartOffset + textPoint.X;
        if (options.IsClosedLoop)
        {
            distance = NormalizeClosedPathDistance(distance, pathLength);
        }
        else if (distance < 0f)
        {
            distance = 0f;
        }
        else if (distance > pathLength)
        {
            distance = pathLength;
        }

        if (!TryGetPointAndTangent(pathSamples, distance, out var rawPoint, out var tangent))
        {
            return false;
        }

        var normal = Normalize(new SKPoint(-tangent.Y, tangent.X));
        var vOffset = options.BaseVOffset + textPoint.Y;
        mappedPoint = new SKPoint(
            rawPoint.X + (normal.X * vOffset),
            rawPoint.Y + (normal.Y * vOffset));
        return true;
    }

    internal static bool TryGetCurrentPosition(
        IReadOnlyList<PathSample> pathSamples,
        float distance,
        float vOffset,
        bool isClosedLoop,
        out SKPoint point,
        out SKPoint tangent)
    {
        point = default;
        tangent = new SKPoint(1f, 0f);
        if (pathSamples.Count == 0)
        {
            return false;
        }

        if (pathSamples.Count == 1)
        {
            point = pathSamples[0].Point;
            return true;
        }

        var pathLength = ResolvePathLength(pathSamples, 0f);
        var sampleDistance = isClosedLoop
            ? NormalizeClosedPathDistance(distance, pathLength)
            : distance;
        if (!TryGetPointAndTangent(pathSamples, sampleDistance, out var rawPoint, out tangent))
        {
            return false;
        }

        if (!isClosedLoop)
        {
            if (distance < 0f)
            {
                rawPoint = new SKPoint(
                    rawPoint.X + (tangent.X * distance),
                    rawPoint.Y + (tangent.Y * distance));
            }
            else if (distance > pathLength)
            {
                var overshoot = distance - pathLength;
                rawPoint = new SKPoint(
                    rawPoint.X + (tangent.X * overshoot),
                    rawPoint.Y + (tangent.Y * overshoot));
            }
        }

        var normal = Normalize(new SKPoint(-tangent.Y, tangent.X));
        point = new SKPoint(
            rawPoint.X + (normal.X * vOffset),
            rawPoint.Y + (normal.Y * vOffset));
        return true;
    }

    internal static bool TryProjectPointOntoPath(
        IReadOnlyList<PathSample> pathSamples,
        SKPoint targetPoint,
        out float distance,
        out float vOffset)
    {
        distance = 0f;
        vOffset = 0f;
        if (pathSamples.Count < 2)
        {
            return false;
        }

        var bestDistanceSquared = float.PositiveInfinity;
        var found = false;
        for (var i = 1; i < pathSamples.Count; i++)
        {
            var left = pathSamples[i - 1];
            var right = pathSamples[i];
            if (right.StartsSubpath)
            {
                continue;
            }

            var segment = new SKPoint(
                right.Point.X - left.Point.X,
                right.Point.Y - left.Point.Y);
            var segmentLengthSquared = (segment.X * segment.X) + (segment.Y * segment.Y);
            if (segmentLengthSquared <= TangentEpsilon * TangentEpsilon)
            {
                continue;
            }

            var pointVector = new SKPoint(
                targetPoint.X - left.Point.X,
                targetPoint.Y - left.Point.Y);
            var t = ClampFloat(
                ((pointVector.X * segment.X) + (pointVector.Y * segment.Y)) / segmentLengthSquared,
                0f,
                1f);
            var closestPoint = new SKPoint(
                left.Point.X + (segment.X * t),
                left.Point.Y + (segment.Y * t));
            var delta = new SKPoint(
                targetPoint.X - closestPoint.X,
                targetPoint.Y - closestPoint.Y);
            var candidateDistanceSquared = (delta.X * delta.X) + (delta.Y * delta.Y);
            if (candidateDistanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            var tangentCandidate = Normalize(segment);
            var normal = new SKPoint(-tangentCandidate.Y, tangentCandidate.X);
            var sampleAdvance = right.Distance - left.Distance;
            bestDistanceSquared = candidateDistanceSquared;
            distance = left.Distance + (sampleAdvance * t);
            vOffset = (delta.X * normal.X) + (delta.Y * normal.Y);
            found = true;
        }

        return found;
    }

    internal static bool TryApplyUserSpaceOffset(
        IReadOnlyList<PathSample> pathSamples,
        float currentOffset,
        float currentVOffset,
        float dx,
        float dy,
        bool isClosedLoop,
        out float nextOffset,
        out float nextVOffset)
    {
        nextOffset = currentOffset;
        nextVOffset = currentVOffset;
        if (Math.Abs(dx) <= 0.001f && Math.Abs(dy) <= 0.001f)
        {
            return true;
        }

        if (!TryGetCurrentPosition(pathSamples, currentOffset, currentVOffset, isClosedLoop, out var currentPoint, out _) ||
            !TryProjectPointOntoPath(pathSamples, new SKPoint(currentPoint.X + dx, currentPoint.Y + dy), out nextOffset, out nextVOffset))
        {
            nextOffset = currentOffset + dx;
            nextVOffset = currentVOffset + dy;
        }

        return true;
    }

    internal static bool TryGetPointAndTangent(
        IReadOnlyList<PathSample> pathSamples,
        float distance,
        out SKPoint point,
        out SKPoint tangent)
    {
        var preferredSegmentIndex = 1;
        return TryGetPointAndTangent(pathSamples, distance, ref preferredSegmentIndex, out point, out tangent);
    }

    internal static bool TryGetPointAndTangent(
        IReadOnlyList<PathSample> pathSamples,
        float distance,
        ref int preferredSegmentIndex,
        out SKPoint point,
        out SKPoint tangent)
    {
        point = default;
        tangent = new SKPoint(1f, 0f);
        if (pathSamples.Count == 0)
        {
            return false;
        }

        if (pathSamples.Count == 1)
        {
            point = pathSamples[0].Point;
            preferredSegmentIndex = 1;
            return true;
        }

        if (distance <= 0f)
        {
            point = pathSamples[0].Point;
            tangent = GetPathStartTangent(pathSamples);
            preferredSegmentIndex = FindNextUsablePathSegmentIndex(pathSamples, 1);
            return true;
        }

        var lastIndex = pathSamples.Count - 1;
        if (distance >= pathSamples[lastIndex].Distance)
        {
            point = pathSamples[lastIndex].Point;
            tangent = GetPathEndTangent(pathSamples);
            preferredSegmentIndex = lastIndex;
            return true;
        }

        preferredSegmentIndex = ResolvePathSegmentIndex(pathSamples, distance, preferredSegmentIndex);
        var previous = pathSamples[preferredSegmentIndex - 1];
        var current = pathSamples[preferredSegmentIndex];
        var segmentLength = current.Distance - previous.Distance;
        if (segmentLength <= 0f)
        {
            point = current.Point;
            tangent = ResolveSegmentTangent(pathSamples, preferredSegmentIndex);
            return true;
        }

        var t = (distance - previous.Distance) / segmentLength;
        var deltaX = current.Point.X - previous.Point.X;
        var deltaY = current.Point.Y - previous.Point.Y;
        point = new SKPoint(
            previous.Point.X + (deltaX * t),
            previous.Point.Y + (deltaY * t));
        tangent = ResolveSegmentTangent(pathSamples, preferredSegmentIndex);
        return true;
    }

    private static bool TryAppendStretchedEllipse(
        SKRect rect,
        IReadOnlyList<PathSample> pathSamples,
        MappingOptions options,
        SKPath stretchedPath)
    {
        if (!IsRenderableRect(rect))
        {
            return false;
        }

        const int segmentCount = 16;
        var centerX = (rect.Left + rect.Right) * 0.5f;
        var centerY = (rect.Top + rect.Bottom) * 0.5f;
        var radiusX = rect.Width * 0.5f;
        var radiusY = rect.Height * 0.5f;
        for (var i = 0; i <= segmentCount; i++)
        {
            var angle = (Math.PI * 2d * i) / segmentCount;
            var textPoint = new SKPoint(
                centerX + (radiusX * (float)Math.Cos(angle)),
                centerY + (radiusY * (float)Math.Sin(angle)));
            if (!TryMapTextPoint(textPoint, pathSamples, options, out var mappedPoint))
            {
                return false;
            }

            if (i == 0)
            {
                stretchedPath.MoveTo(mappedPoint.X, mappedPoint.Y);
            }
            else
            {
                stretchedPath.LineTo(mappedPoint.X, mappedPoint.Y);
            }
        }

        stretchedPath.Close();
        return true;
    }

    private static bool TryAppendStretchedPoly(
        IList<SKPoint>? points,
        bool close,
        IReadOnlyList<PathSample> pathSamples,
        MappingOptions options,
        SKPath stretchedPath)
    {
        if (points is null || points.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < points.Count; i++)
        {
            if (!TryMapTextPoint(points[i], pathSamples, options, out var mappedPoint))
            {
                return false;
            }

            if (i == 0)
            {
                stretchedPath.MoveTo(mappedPoint.X, mappedPoint.Y);
            }
            else
            {
                stretchedPath.LineTo(mappedPoint.X, mappedPoint.Y);
            }
        }

        if (close)
        {
            stretchedPath.Close();
        }

        return true;
    }

    private static bool TryAppendStretchedRect(
        SKRect rect,
        IReadOnlyList<PathSample> pathSamples,
        MappingOptions options,
        SKPath stretchedPath)
    {
        if (!TryMapTextPoint(rect.TopLeft, pathSamples, options, out var topLeft) ||
            !TryMapTextPoint(rect.TopRight, pathSamples, options, out var topRight) ||
            !TryMapTextPoint(rect.BottomRight, pathSamples, options, out var bottomRight) ||
            !TryMapTextPoint(rect.BottomLeft, pathSamples, options, out var bottomLeft))
        {
            return false;
        }

        stretchedPath.MoveTo(topLeft.X, topLeft.Y);
        stretchedPath.LineTo(topRight.X, topRight.Y);
        stretchedPath.LineTo(bottomRight.X, bottomRight.Y);
        stretchedPath.LineTo(bottomLeft.X, bottomLeft.Y);
        stretchedPath.Close();
        return true;
    }

    private static bool IsRenderableRect(SKRect rect)
    {
        return !float.IsNaN(rect.Left) &&
               !float.IsNaN(rect.Top) &&
               !float.IsNaN(rect.Right) &&
               !float.IsNaN(rect.Bottom) &&
               !float.IsInfinity(rect.Left) &&
               !float.IsInfinity(rect.Top) &&
               !float.IsInfinity(rect.Right) &&
               !float.IsInfinity(rect.Bottom) &&
               Math.Abs(rect.Width) > TangentEpsilon &&
               Math.Abs(rect.Height) > TangentEpsilon;
    }

    private static List<StretchTextClusterRange> CreateTextElementRanges(string text)
    {
        var ranges = new List<StretchTextClusterRange>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var start = enumerator.ElementIndex;
            if (enumerator.GetTextElement() is not { Length: > 0 } textElement)
            {
                continue;
            }

            ranges.Add(new StretchTextClusterRange(start, start + textElement.Length));
        }

        return ranges;
    }

    private static bool TryFindTextElementRange(
        IReadOnlyList<StretchTextClusterRange> ranges,
        int charIndex,
        out StretchTextClusterRange range)
    {
        for (var i = 0; i < ranges.Count; i++)
        {
            var candidate = ranges[i];
            if (charIndex >= candidate.Start && charIndex < candidate.End)
            {
                range = candidate;
                return true;
            }

            if (charIndex < candidate.Start)
            {
                break;
            }
        }

        range = default;
        return false;
    }

    private static SKPoint RotateTangent(SKPoint tangent, float rotationDegrees)
    {
        if (Math.Abs(rotationDegrees) <= 0.001f)
        {
            return tangent;
        }

        var rotationRadians = rotationDegrees * ((float)Math.PI / 180f);
        var cos = (float)Math.Cos(rotationRadians);
        var sin = (float)Math.Sin(rotationRadians);
        return new SKPoint(
            (tangent.X * cos) - (tangent.Y * sin),
            (tangent.X * sin) + (tangent.Y * cos));
    }

    private static float ResolvePathLength(IReadOnlyList<PathSample> pathSamples, float pathLength)
    {
        if (pathLength > 0f)
        {
            return pathLength;
        }

        return pathSamples.Count > 0 ? pathSamples[pathSamples.Count - 1].Distance : 0f;
    }

    private static float NormalizeClosedPathDistance(float distance, float pathLength)
    {
        if (pathLength <= 0f)
        {
            return distance;
        }

        var normalized = distance % pathLength;
        return normalized < 0f
            ? normalized + pathLength
            : normalized;
    }

    private static int ResolvePathSegmentIndex(IReadOnlyList<PathSample> pathSamples, float distance, int preferredSegmentIndex)
    {
        var count = pathSamples.Count;
        preferredSegmentIndex = FindNextUsablePathSegmentIndex(pathSamples, preferredSegmentIndex < 1 ? 1 : preferredSegmentIndex);
        if (preferredSegmentIndex < count)
        {
            var previous = pathSamples[preferredSegmentIndex - 1];
            var current = pathSamples[preferredSegmentIndex];
            if (distance >= previous.Distance && distance <= current.Distance)
            {
                return preferredSegmentIndex;
            }

            if (distance > current.Distance)
            {
                return BinarySearchPathSegmentIndex(pathSamples, distance, preferredSegmentIndex + 1, count - 1);
            }
        }

        return BinarySearchPathSegmentIndex(pathSamples, distance, 1, count - 1);
    }

    private static int BinarySearchPathSegmentIndex(IReadOnlyList<PathSample> pathSamples, float distance, int low, int high)
    {
        if (low > high)
        {
            return pathSamples.Count - 1;
        }

        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (pathSamples[mid].Distance < distance)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return FindNextUsablePathSegmentIndex(pathSamples, low);
    }

    private static int FindNextUsablePathSegmentIndex(IReadOnlyList<PathSample> pathSamples, int startIndex)
    {
        for (var i = Math.Max(1, startIndex); i < pathSamples.Count; i++)
        {
            if (!pathSamples[i].StartsSubpath)
            {
                return i;
            }
        }

        return pathSamples.Count - 1;
    }

    private static SKPoint GetPathStartTangent(IReadOnlyList<PathSample> pathSamples)
    {
        for (var i = 1; i < pathSamples.Count; i++)
        {
            if (!TryGetSegmentTangent(pathSamples, i, out var tangent))
            {
                continue;
            }

            return tangent;
        }

        return new SKPoint(1f, 0f);
    }

    private static SKPoint GetPathEndTangent(IReadOnlyList<PathSample> pathSamples)
    {
        for (var i = pathSamples.Count - 1; i >= 1; i--)
        {
            if (!TryGetSegmentTangent(pathSamples, i, out var tangent))
            {
                continue;
            }

            return tangent;
        }

        return new SKPoint(1f, 0f);
    }

    private static SKPoint ResolveSegmentTangent(IReadOnlyList<PathSample> pathSamples, int segmentIndex)
    {
        if (TryGetSegmentTangent(pathSamples, segmentIndex, out var tangent))
        {
            return tangent;
        }

        for (var i = segmentIndex + 1; i < pathSamples.Count; i++)
        {
            if (TryGetSegmentTangent(pathSamples, i, out tangent))
            {
                return tangent;
            }
        }

        for (var i = segmentIndex - 1; i >= 1; i--)
        {
            if (TryGetSegmentTangent(pathSamples, i, out tangent))
            {
                return tangent;
            }
        }

        return new SKPoint(1f, 0f);
    }

    private static bool TryGetSegmentTangent(IReadOnlyList<PathSample> pathSamples, int segmentIndex, out SKPoint tangent)
    {
        tangent = new SKPoint(1f, 0f);
        if (segmentIndex <= 0 ||
            segmentIndex >= pathSamples.Count ||
            pathSamples[segmentIndex].StartsSubpath)
        {
            return false;
        }

        return TryNormalize(
            new SKPoint(
                pathSamples[segmentIndex].Point.X - pathSamples[segmentIndex - 1].Point.X,
                pathSamples[segmentIndex].Point.Y - pathSamples[segmentIndex - 1].Point.Y),
            out tangent);
    }

    private static SKPoint Normalize(SKPoint point)
    {
        return TryNormalize(point, out var normalized)
            ? normalized
            : new SKPoint(1f, 0f);
    }

    private static bool TryNormalize(SKPoint point, out SKPoint normalized)
    {
        var length = (float)Math.Sqrt((point.X * point.X) + (point.Y * point.Y));
        if (length <= TangentEpsilon)
        {
            normalized = new SKPoint(1f, 0f);
            return false;
        }

        normalized = new SKPoint(point.X / length, point.Y / length);
        return true;
    }

    private static float ClampFloat(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static bool IsValidPositiveAdvance(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
