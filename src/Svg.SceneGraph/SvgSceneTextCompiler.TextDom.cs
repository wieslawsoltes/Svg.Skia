using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static partial class SvgSceneTextCompiler
{
    internal sealed class SvgTextContentMetrics
    {
        private readonly TextDomClusterMetric[] _clusters;
        private readonly int _numberOfChars;
        private readonly bool _hasHitTestCells;
        private readonly TextDomHitCell[]? _extraHitCells;
        private TextDomHitCell[]? _hitCells;

        internal static SvgTextContentMetrics Empty { get; } = new(Array.Empty<TextDomClusterMetric>(), 0, 0f, Array.Empty<TextDomHitCell>());

        public SvgTextContentMetrics(
            TextDomClusterMetric[] clusters,
            int numberOfChars,
            float computedTextLength)
            : this(clusters, numberOfChars, computedTextLength, null)
        {
        }

        internal SvgTextContentMetrics(
            TextDomClusterMetric[] clusters,
            int numberOfChars,
            float computedTextLength,
            TextDomHitCell[]? hitCells)
        {
            _clusters = clusters;
            _extraHitCells = hitCells is { Length: > 0 } ? hitCells : null;
            _hasHitTestCells = _extraHitCells is { Length: > 0 } || HasHitCellCandidate(clusters);
            _numberOfChars = numberOfChars;
            ComputedTextLength = computedTextLength;
        }

        public int NumberOfChars => _numberOfChars;

        public float ComputedTextLength { get; }

        internal bool HasHitTestCells => _hasHitTestCells;

        public float GetSubStringLength(int charnum, int nchars)
        {
            ValidateCharacterIndex(charnum);
            if (nchars < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nchars));
            }

            if (nchars == 0)
            {
                return 0f;
            }

            var endCharIndex = GetClampedEndCharIndex(charnum, nchars);
            var minStartOffset = float.PositiveInfinity;
            var maxEndOffset = float.NegativeInfinity;
            for (var i = 0; i < _clusters.Length; i++)
            {
                var cluster = _clusters[i];
                var clusterEndCharIndex = cluster.StartCharIndex + cluster.CharLength;
                if (cluster.StartCharIndex >= endCharIndex)
                {
                    break;
                }

                if (clusterEndCharIndex <= charnum)
                {
                    continue;
                }

                minStartOffset = Math.Min(minStartOffset, cluster.StartOffset);
                maxEndOffset = Math.Max(maxEndOffset, cluster.EndOffset);
            }

            return float.IsInfinity(minStartOffset) || float.IsInfinity(maxEndOffset)
                ? 0f
                : Math.Max(0f, maxEndOffset - minStartOffset);
        }

        public SKPoint GetStartPositionOfChar(int charnum)
        {
            var cluster = GetCluster(charnum);
            return cluster.StartPoint;
        }

        public SKPoint GetEndPositionOfChar(int charnum)
        {
            var cluster = GetCluster(charnum);
            return cluster.EndPoint;
        }

        public SKRect GetExtentOfChar(int charnum)
        {
            return GetCluster(charnum).Extent;
        }

        public bool TryGetCaretMetadata(int charnum, out SKPoint position, out SKRect extent)
        {
            position = default;
            extent = default;

            if (NumberOfChars == 0 ||
                charnum < 0 ||
                charnum > NumberOfChars)
            {
                return false;
            }

            if (charnum == NumberOfChars)
            {
                var lastCluster = GetCluster(NumberOfChars - 1);
                position = lastCluster.EndPoint;
                extent = lastCluster.Extent;
                return true;
            }

            var cluster = GetCluster(charnum);
            position = cluster.StartPoint;
            extent = cluster.Extent;
            return true;
        }

        public float GetRotationOfChar(int charnum)
        {
            return GetCluster(charnum).RotationDegrees;
        }

        public int GetCharNumAtPosition(SKPoint point)
        {
            for (var clusterIndex = 0; clusterIndex < _clusters.Length; clusterIndex++)
            {
                if (!TryGetCharacterIndexAtPosition(_clusters[clusterIndex], point, out var charIndex))
                {
                    continue;
                }

                return charIndex;
            }

            return -1;
        }

        internal bool HitTestCharacterCell(SKPoint point)
        {
            if (!_hasHitTestCells)
            {
                return false;
            }

            var hitCells = GetHitCells();
            for (var i = 0; i < hitCells.Length; i++)
            {
                if (ContainsPoint(hitCells[i].Extent, point))
                {
                    return true;
                }
            }

            return false;
        }

        public SKRect[] GetSelectionExtents(int charnum, int nchars)
        {
            ValidateCharacterIndex(charnum);
            if (nchars < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nchars));
            }

            if (nchars == 0)
            {
                return Array.Empty<SKRect>();
            }

            var endCharIndex = GetClampedEndCharIndex(charnum, nchars);
            var extents = new List<SKRect>();
            for (var i = 0; i < _clusters.Length; i++)
            {
                var cluster = _clusters[i];
                var clusterEndCharIndex = cluster.StartCharIndex + cluster.CharLength;
                if (cluster.StartCharIndex >= endCharIndex)
                {
                    break;
                }

                if (clusterEndCharIndex <= charnum ||
                    cluster.Extent.IsEmpty)
                {
                    continue;
                }

                extents.Add(cluster.Extent);
            }

            return MergeSelectionExtents(extents);
        }

        private TextDomClusterMetric GetCluster(int charnum)
        {
            ValidateCharacterIndex(charnum);
            var low = 0;
            var high = _clusters.Length - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) >> 1);
                var cluster = _clusters[mid];
                if (charnum < cluster.StartCharIndex)
                {
                    high = mid - 1;
                    continue;
                }

                if (charnum >= cluster.StartCharIndex + cluster.CharLength)
                {
                    low = mid + 1;
                    continue;
                }

                return cluster;
            }

            throw new ArgumentOutOfRangeException(nameof(charnum));
        }

        private TextDomHitCell[] GetHitCells()
        {
            var hitCells = _hitCells;
            if (hitCells is not null)
            {
                return hitCells;
            }

            hitCells = CreateHitCells(_clusters, _extraHitCells);
            return Interlocked.CompareExchange(ref _hitCells, hitCells, null) ?? hitCells;
        }

        private void ValidateCharacterIndex(int charnum)
        {
            if (charnum < 0 || charnum >= NumberOfChars)
            {
                throw new ArgumentOutOfRangeException(nameof(charnum));
            }
        }

        private int GetClampedEndCharIndex(int charnum, int nchars)
        {
            var requestedEndCharIndex = (long)charnum + nchars;
            return requestedEndCharIndex >= NumberOfChars
                ? NumberOfChars
                : (int)requestedEndCharIndex;
        }

        private static bool TryGetCharacterIndexAtPosition(TextDomClusterMetric cluster, SKPoint point, out int charIndex)
        {
            charIndex = -1;
            if (cluster.CharLength <= 0 ||
                cluster.Extent.IsEmpty)
            {
                return false;
            }

            var advance = cluster.EndOffset - cluster.StartOffset;
            if (advance <= 0f)
            {
                return false;
            }

            var dx = cluster.EndPoint.X - cluster.StartPoint.X;
            var dy = cluster.EndPoint.Y - cluster.StartPoint.Y;
            var distance = (float)Math.Sqrt((dx * dx) + (dy * dy));
            if (distance <= 0f)
            {
                return false;
            }

            var directionX = dx / distance;
            var directionY = dy / distance;
            var pointOffsetX = point.X - cluster.StartPoint.X;
            var pointOffsetY = point.Y - cluster.StartPoint.Y;
            var inlineOffset = (pointOffsetX * directionX) + (pointOffsetY * directionY);
            if (inlineOffset < 0f || inlineOffset > distance)
            {
                return false;
            }

            var perpendicularDistance = Math.Abs((pointOffsetX * -directionY) + (pointOffsetY * directionX));
            var hitHalfThickness = Math.Max(
                TextLengthTolerance,
                Math.Min(32f, Math.Max(Math.Abs(cluster.Extent.Width), Math.Abs(cluster.Extent.Height)) * 0.5f));
            if (perpendicularDistance > hitHalfThickness)
            {
                return false;
            }

            var segmentLength = distance / cluster.CharLength;
            if (segmentLength <= 0f)
            {
                charIndex = cluster.StartCharIndex;
                return true;
            }

            var characterOffset = (int)Math.Floor(inlineOffset / segmentLength);
            if (characterOffset >= cluster.CharLength)
            {
                characterOffset = cluster.CharLength - 1;
            }

            charIndex = cluster.StartCharIndex + characterOffset;
            return true;
        }

        private static TextDomHitCell[] CreateHitCells(
            IReadOnlyList<TextDomClusterMetric> clusters,
            TextDomHitCell[]? extraHitCells)
        {
            var extraHitCellCount = extraHitCells?.Length ?? 0;
            if (clusters.Count == 0)
            {
                return extraHitCellCount == 0 ? Array.Empty<TextDomHitCell>() : extraHitCells!;
            }

            var hitCells = new List<TextDomHitCell>(clusters.Count + extraHitCellCount);
            if (extraHitCells is not null)
            {
                hitCells.AddRange(extraHitCells);
            }

            for (var i = 0; i < clusters.Count; i++)
            {
                if (!clusters[i].HitExtent.IsEmpty)
                {
                    hitCells.Add(new TextDomHitCell(clusters[i].HitExtent));
                }
            }

            return hitCells.ToArray();
        }

        private static bool HasHitCellCandidate(IReadOnlyList<TextDomClusterMetric> clusters)
        {
            for (var i = 0; i < clusters.Count; i++)
            {
                if (!clusters[i].HitExtent.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPoint(SKRect bounds, SKPoint point)
        {
            if (bounds.IsEmpty)
            {
                return false;
            }

            const float tolerance = 0.25f;
            return point.X >= bounds.Left - tolerance &&
                   point.X <= bounds.Right + tolerance &&
                   point.Y >= bounds.Top - tolerance &&
                   point.Y <= bounds.Bottom + tolerance;
        }

        private static SKRect[] MergeSelectionExtents(List<SKRect> extents)
        {
            if (extents.Count <= 1)
            {
                return extents.ToArray();
            }

            var merged = new List<SKRect>(extents.Count);
            for (var i = 0; i < extents.Count; i++)
            {
                var extent = extents[i];
                if (extent.IsEmpty)
                {
                    continue;
                }

                if (merged.Count == 0)
                {
                    merged.Add(extent);
                    continue;
                }

                var lastIndex = merged.Count - 1;
                var last = merged[lastIndex];
                if (AreSameSelectionLine(last, extent))
                {
                    merged[lastIndex] = Union(last, extent);
                    continue;
                }

                merged.Add(extent);
            }

            return merged.ToArray();
        }

        private static bool AreSameSelectionLine(SKRect left, SKRect right)
        {
            var verticalOverlap = Math.Min(left.Bottom, right.Bottom) - Math.Max(left.Top, right.Top);
            var minHeight = Math.Min(Math.Abs(left.Height), Math.Abs(right.Height));
            if (minHeight <= 0f || verticalOverlap < minHeight * 0.5f)
            {
                return false;
            }

            var gap = Math.Max(0f, Math.Max(left.Left, right.Left) - Math.Min(left.Right, right.Right));
            return gap <= Math.Max(TextLengthTolerance, minHeight * 0.25f);
        }

        private static SKRect Union(SKRect left, SKRect right)
        {
            return new SKRect(
                Math.Min(left.Left, right.Left),
                Math.Min(left.Top, right.Top),
                Math.Max(left.Right, right.Right),
                Math.Max(left.Bottom, right.Bottom));
        }
    }

    private sealed class SvgTextContentMetricsBuilder
    {
        private readonly List<TextDomClusterMetric> _clusters = new();
        private List<TextDomHitCell>? _extraHitCells;
        private int _numberOfChars;
        private float _computedTextLength;

        public void AppendRun(IReadOnlyList<TextDomRunClusterMetric> runClusters, float runLength)
        {
            var runStartOffset = _computedTextLength;
            for (var clusterIndex = 0; clusterIndex < runClusters.Count; clusterIndex++)
            {
                var runCluster = runClusters[clusterIndex];
                if (runCluster.CharLength <= 0)
                {
                    AppendExtraHitCell(runCluster);
                    continue;
                }

                var startCharIndex = _numberOfChars;
                var charLength = runCluster.CharLength;
                if (runCluster.StartCharIndex is { } explicitStartCharIndex)
                {
                    startCharIndex = explicitStartCharIndex;
                }

                var globalCluster = new TextDomClusterMetric(
                    startCharIndex,
                    charLength,
                    runStartOffset + runCluster.StartOffset,
                    runStartOffset + runCluster.EndOffset,
                    runCluster.StartPoint,
                    runCluster.EndPoint,
                    runCluster.Extent,
                    runCluster.RotationDegrees)
                {
                    HitExtent = runCluster.HitExtent
                };
                _clusters.Add(globalCluster);
                _numberOfChars = runCluster.StartCharIndex is { }
                    ? Math.Max(_numberOfChars, startCharIndex + charLength)
                    : _numberOfChars + charLength;
            }

            _computedTextLength = runStartOffset + Math.Max(0f, runLength);
        }

        public void AppendRunAtCharacterIndex(
            int startCharIndex,
            IReadOnlyList<TextDomRunClusterMetric> runClusters,
            float runStartOffset,
            float runLength)
        {
            var localCharIndex = 0;
            for (var clusterIndex = 0; clusterIndex < runClusters.Count; clusterIndex++)
            {
                var runCluster = runClusters[clusterIndex];
                if (runCluster.CharLength <= 0)
                {
                    AppendExtraHitCell(runCluster);
                    continue;
                }

                var charLength = runCluster.CharLength;
                var globalCluster = new TextDomClusterMetric(
                    startCharIndex + localCharIndex,
                    charLength,
                    runStartOffset + runCluster.StartOffset,
                    runStartOffset + runCluster.EndOffset,
                    runCluster.StartPoint,
                    runCluster.EndPoint,
                    runCluster.Extent,
                    runCluster.RotationDegrees)
                {
                    HitExtent = runCluster.HitExtent
                };
                _clusters.Add(globalCluster);
                localCharIndex += charLength;
            }

            _numberOfChars = Math.Max(_numberOfChars, startCharIndex + localCharIndex);
            _computedTextLength = Math.Max(_computedTextLength, runStartOffset + Math.Max(0f, runLength));
        }

        public SvgTextContentMetrics Build()
        {
            _clusters.Sort(static (left, right) => left.StartCharIndex.CompareTo(right.StartCharIndex));
            return new SvgTextContentMetrics(
                _clusters.ToArray(),
                _numberOfChars,
                _computedTextLength,
                _extraHitCells?.ToArray());
        }

        private void AppendExtraHitCell(TextDomRunClusterMetric runCluster)
        {
            if (runCluster.HitExtent.IsEmpty)
            {
                return;
            }

            (_extraHitCells ??= new List<TextDomHitCell>()).Add(new TextDomHitCell(runCluster.HitExtent));
        }
    }

    internal readonly record struct TextDomClusterMetric(
        int StartCharIndex,
        int CharLength,
        float StartOffset,
        float EndOffset,
        SKPoint StartPoint,
        SKPoint EndPoint,
        SKRect Extent,
        float RotationDegrees)
    {
        public SKRect HitExtent { get; init; } = Extent;
    }

    private readonly record struct TextDomRunClusterMetric(
        int CharLength,
        float StartOffset,
        float EndOffset,
        SKPoint StartPoint,
        SKPoint EndPoint,
        SKRect Extent,
        float RotationDegrees)
    {
        public SKRect HitExtent { get; init; } = Extent;

        public int? StartCharIndex { get; init; }
    }

    internal readonly record struct TextDomHitCell(SKRect Extent);

    private readonly record struct TextDomClusterSource(
        int FirstCodepointIndex,
        string Text,
        float RelativeOffset,
        float Advance)
    {
        public int? FirstCharIndex { get; init; }
    }

    private readonly record struct SequentialTextContentRun(
        SvgTextBase StyleSource,
        string Text,
        float[]? Rotations);

    internal static bool TryCreateTextContentMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SvgTextContentMetrics metrics)
    {
        metrics = SvgTextContentMetrics.Empty;
        if (TryCreateSharedInlineSizeTextContentMetrics(svgTextBase, viewport, assetLoader, out metrics))
        {
            return true;
        }

        if (TryCreateWrappedInlineSizeTextLengthTextContentMetrics(svgTextBase, viewport, assetLoader, out metrics))
        {
            return true;
        }

        if (TryCreateFlattenedTextLengthTextContentMetrics(svgTextBase, viewport, assetLoader, out metrics))
        {
            return true;
        }

        if (TryCreateInlineSizeTextPathTextContentMetrics(svgTextBase, viewport, assetLoader, out metrics))
        {
            return true;
        }

        if (TryCreateInlineSizeTextContentMetrics(svgTextBase, viewport, assetLoader, out metrics))
        {
            return true;
        }

        if (TryCreateBidiSequentialTextContentMetrics(svgTextBase, viewport, assetLoader, out metrics))
        {
            return true;
        }

        if (TryCreateSequentialTextContentMetrics(svgTextBase, viewport, assetLoader, out metrics))
        {
            return true;
        }

        var builder = new SvgTextContentMetricsBuilder();
        var xs = new List<float>();
        var ys = new List<float>();
        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
        var currentX = xs.Count >= 1 ? xs[0] : 0f;
        var currentY = ys.Count >= 1 ? ys[0] : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        currentX += baselineShift.X;
        currentY += baselineShift.Y;
        ApplyInitialChildContainerOffsets(svgTextBase, viewport, assetLoader, ref currentX, ref currentY);
        if (!TryCollectTextContentMetricsBase(
                svgTextBase,
                ref currentX,
                ref currentY,
                viewport,
                assetLoader,
                inheritedRotationState: null,
                inheritedAbsolutePositionState: null,
                trimLeadingWhitespaceAtStart: true,
                builder))
        {
            return false;
        }

        metrics = builder.Build();
        return true;
    }

    private static bool TryCreateInlineSizeTextContentMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SvgTextContentMetrics metrics)
    {
        metrics = SvgTextContentMetrics.Empty;
        if (!HasInlineSizeLayout(svgTextBase) ||
            ResolveTextOverflowMarker(svgTextBase) is not null)
        {
            return false;
        }

        var preservePreLineBreaks = HasInlineSizeLayout(svgTextBase) && PreservesInlineLineBreaksInTextSubtree(svgTextBase);
        if (!TryCollectSequentialTextRuns(
                svgTextBase,
                requireAnchorContent: false,
                IsTextReferenceRenderingEnabled(assetLoader),
                trimLeadingWhitespaceAtStart: true,
                out var runs,
                preservePreLineBreaks))
        {
            return false;
        }

        if (HasInlineSizeTextContentMetricBarriers(svgTextBase, runs))
        {
            return false;
        }

        var xs = new List<float>();
        var ys = new List<float>();
        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
        var currentX = xs.Count >= 1 ? xs[0] : 0f;
        var currentY = ys.Count >= 1 ? ys[0] : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        currentX += baselineShift.X;
        currentY += baselineShift.Y;

        var geometryBounds = EstimateGeometryBounds(svgTextBase, viewport, assetLoader);
        if (geometryBounds.IsEmpty)
        {
            geometryBounds = viewport;
        }

        if (!TryCreateInlineSizeTextOverflowLayout(svgTextBase, runs, currentX, currentY, viewport, geometryBounds, assetLoader, out var layout) ||
            layout is null)
        {
            return false;
        }

        var builder = new SvgTextContentMetricsBuilder();
        var lineStartCharIndex = 0;
        var lineStartOffset = 0f;
        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            if (UsesVisualInlineSizeTextRunOrder(line))
            {
                var runStartOffset = 0f;
                var visualDrawX = line.StartX;
                var visualDrawY = line.BaselineY;
                var cursorX = line.PlaceVisualRunsRightToLeft ? GetInlineSizeVisualRunCursorX(line) : line.StartX;
                for (var runIndex = 0; runIndex < line.VisualRuns.Count; runIndex++)
                {
                    var run = line.VisualRuns[runIndex];
                    if (run.SourceCodepointIndex < 0)
                    {
                        return false;
                    }

                    if (!TryCreateTextRunMetrics(
                            run.StyleSource,
                            run.Text,
                            line.PlaceVisualRunsRightToLeft ? TakeInlineSizeVisualRunX(line, run, ref cursorX) : visualDrawX,
                            visualDrawY,
                            viewport,
                            assetLoader,
                            rotations: null,
                            forceLeftAlign: true,
                            out var clusters,
                            out var runLength))
                    {
                        return false;
                    }

                    builder.AppendRunAtCharacterIndex(
                        lineStartCharIndex + run.SourceCodepointIndex,
                        line.ShouldClip ? ClipTextDomRunClusters(clusters, line.ClipRect) : clusters,
                        lineStartOffset + runStartOffset,
                        runLength);
                    if (!line.PlaceVisualRunsRightToLeft)
                    {
                        ApplyInlineAdvance(run.StyleSource, ref visualDrawX, ref visualDrawY, runLength);
                    }

                    runStartOffset += runLength;
                }

                lineStartCharIndex += GetInlineSizeRunTextLength(line.Runs);
                lineStartOffset += line.LogicalAdvance;
                continue;
            }

            var drawX = line.StartX;
            var drawY = line.BaselineY;
            for (var runIndex = 0; runIndex < line.Runs.Count; runIndex++)
            {
                var run = line.Runs[runIndex];
                if (!TryCreateTextRunMetrics(
                        run.StyleSource,
                        run.Text,
                        drawX,
                        drawY,
                        viewport,
                        assetLoader,
                        rotations: null,
                        forceLeftAlign: true,
                        out var clusters,
                        out var runLength))
                {
                    return false;
                }

                builder.AppendRun(line.ShouldClip ? ClipTextDomRunClusters(clusters, line.ClipRect) : clusters, runLength);
                ApplyInlineAdvance(run.StyleSource, ref drawX, ref drawY, runLength);
            }

            lineStartCharIndex += GetInlineSizeRunTextLength(line.Runs);
            lineStartOffset += line.LogicalAdvance;
        }

        metrics = builder.Build();
        return metrics.NumberOfChars > 0;
    }

    private static int GetInlineSizeRunTextLength(IReadOnlyList<InlineSizeTextRun> runs)
    {
        var length = 0;
        for (var i = 0; i < runs.Count; i++)
        {
            length += runs[i].Text.Length;
        }

        return length;
    }

    private static bool UsesVisualInlineSizeTextRunOrder(InlineSizeTextLine line)
    {
        if (line.PlaceVisualRunsRightToLeft)
        {
            return true;
        }

        if (line.VisualRuns.Count != line.Runs.Count)
        {
            return true;
        }

        for (var i = 0; i < line.Runs.Count; i++)
        {
            if (!ReferenceEquals(line.Runs[i].StyleSource, line.VisualRuns[i].StyleSource) ||
                !string.Equals(line.Runs[i].Text, line.VisualRuns[i].Text, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateWrappedInlineSizeTextLengthTextContentMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SvgTextContentMetrics metrics)
    {
        metrics = SvgTextContentMetrics.Empty;
        var xs = new List<float>();
        var ys = new List<float>();
        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
        var currentX = xs.Count >= 1 ? xs[0] : 0f;
        var currentY = ys.Count >= 1 ? ys[0] : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        currentX += baselineShift.X;
        currentY += baselineShift.Y;

        if (!TryCreateWrappedInlineSizeTextLengthLayout(
                svgTextBase,
                currentX,
                currentY,
                viewport,
                viewport,
                assetLoader,
                trimLeadingWhitespaceAtStart: true,
                out var layout) ||
            layout is null)
        {
            return false;
        }

        var builder = new SvgTextContentMetricsBuilder();
        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            var clusters = new List<TextDomRunClusterMetric>();
            for (var runIndex = 0; runIndex < line.Runs.Length; runIndex++)
            {
                var run = line.Runs[runIndex];
                if (!TryCreatePlacedTextRunMetrics(
                        run.StyleSource,
                        run.Text,
                        viewport,
                        assetLoader,
                        run.Placements,
                        line.StartX,
                        out var runClusters,
                        out _))
                {
                    return false;
                }

                clusters.AddRange(runClusters);
            }

            builder.AppendRun(clusters, line.Advance);
        }

        metrics = builder.Build();
        return metrics.NumberOfChars > 0;
    }

    private static bool TryCreateFlattenedTextLengthTextContentMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SvgTextContentMetrics metrics)
    {
        metrics = SvgTextContentMetrics.Empty;
        var xs = new List<float>();
        var ys = new List<float>();
        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
        var currentX = xs.Count >= 1 ? xs[0] : 0f;
        var currentY = ys.Count >= 1 ? ys[0] : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        currentX += baselineShift.X;
        currentY += baselineShift.Y;

        if (!TryCreateFlattenedTextLengthRuns(
                svgTextBase,
                currentX,
                currentY,
                viewport,
                viewport,
                assetLoader,
                trimLeadingWhitespaceAtStart: true,
                out var runs,
                out var totalAdvance,
                out _) ||
            runs.Count == 0)
        {
            return false;
        }

        var globalOriginX = runs[0].Placements[0].Point.X;
        var clusters = new List<TextDomRunClusterMetric>();
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (!TryCreatePlacedTextRunMetrics(
                    run.StyleSource,
                    run.Text,
                    viewport,
                    assetLoader,
                    run.Placements,
                    globalOriginX,
                    out var runClusters,
                    out _))
            {
                return false;
            }

            clusters.AddRange(runClusters);
        }

        if (clusters.Count == 0)
        {
            return false;
        }

        var builder = new SvgTextContentMetricsBuilder();
        builder.AppendRun(clusters, totalAdvance);
        metrics = builder.Build();
        return metrics.NumberOfChars > 0;
    }

    private static bool TryCreateInlineSizeTextPathTextContentMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SvgTextContentMetrics metrics)
    {
        metrics = SvgTextContentMetrics.Empty;
        var currentX = svgTextBase.X.Count >= 1
            ? ResolveTextUnitValue(svgTextBase.X[0], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader)
            : 0f;
        var currentY = svgTextBase.Y.Count >= 1
            ? ResolveTextUnitValue(svgTextBase.Y[0], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader)
            : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        currentX += baselineShift.X;
        currentY += baselineShift.Y;

        if (TryCreateInlineSizeTextPathFlowTextContentMetrics(svgTextBase, currentX, currentY, viewport, assetLoader, out metrics))
        {
            return true;
        }

        if (!TryCreateInlineSizeTextPathFragment(svgTextBase, currentX, currentY, viewport, viewport, assetLoader, out var fragment))
        {
            return false;
        }

        if (fragment.TextPath.Method == SvgTextPathMethod.Stretch)
        {
            return false;
        }

        if (!TryCreateTextPathRunPlacements(
                fragment.Runs,
                fragment.PathSamples,
                fragment.IsClosedLoop,
                fragment.PathOffset,
                fragment.VerticalOffset,
                viewport,
                fragment.GeometryBounds,
                assetLoader,
                out var positionedRuns,
                out _,
                out _))
        {
            return false;
        }

        var builder = new SvgTextContentMetricsBuilder();
        for (var i = 0; i < positionedRuns.Count; i++)
        {
            var run = positionedRuns[i];
            if (!TryCreateTextPathRunMetrics(run.StyleSource, run.Text, viewport, assetLoader, run.Placements, out var clusters, out var runLength))
            {
                return false;
            }

            builder.AppendRun(clusters, runLength);
        }

        metrics = builder.Build();
        return metrics.NumberOfChars > 0;
    }

    private static bool TryCreateInlineSizeTextPathFlowTextContentMetrics(
        SvgTextBase svgTextBase,
        float currentX,
        float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SvgTextContentMetrics metrics)
    {
        metrics = SvgTextContentMetrics.Empty;
        if (!TryCreateInlineSizeTextPathFlowLayout(svgTextBase, currentX, currentY, viewport, viewport, assetLoader, out var layout) ||
            layout is null)
        {
            return false;
        }

        var builder = new SvgTextContentMetricsBuilder();
        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            var drawX = line.StartX;
            var drawY = line.BaselineY;
            for (var runIndex = 0; runIndex < line.RunIndexes.Length; runIndex++)
            {
                var run = layout.Runs[line.RunIndexes[runIndex]];
                if (run.Kind == InlineSizeTextPathFlowRunKind.Text)
                {
                    if (!TryCreateTextRunMetrics(
                            run.StyleSource,
                            run.Text,
                            drawX,
                            drawY,
                            viewport,
                            assetLoader,
                            rotations: null,
                            forceLeftAlign: true,
                            out var clusters,
                            out var runLength))
                    {
                        return false;
                    }

                    builder.AppendRun(clusters, run.Advance);
                    drawX += run.Advance;
                    continue;
                }

                if (run.TextPath is null ||
                    run.TextPath.Method == SvgTextPathMethod.Stretch ||
                    run.TextPathRuns is null ||
                    run.PathSamples is null ||
                    !TryResolveInlineSizeTextPathFlowOffsets(run, drawX, drawY, viewport, assetLoader, out var hOffset, out var verticalOffset) ||
                    !TryCreateTextPathRunPlacements(
                        run.TextPathRuns,
                        run.PathSamples,
                        run.IsClosedLoop,
                        hOffset,
                        verticalOffset,
                        viewport,
                        run.GeometryBounds,
                        assetLoader,
                        out var positionedRuns,
                        out _,
                        out _,
                        run.SpecifiedLengthOverride))
                {
                    return false;
                }

                for (var positionedRunIndex = 0; positionedRunIndex < positionedRuns.Count; positionedRunIndex++)
                {
                    var positionedRun = positionedRuns[positionedRunIndex];
                    if (!TryCreateTextPathRunMetrics(positionedRun.StyleSource, positionedRun.Text, viewport, assetLoader, positionedRun.Placements, out var clusters, out var runLength))
                    {
                        return false;
                    }

                    builder.AppendRun(clusters, runLength);
                }

                drawX += run.Advance;
            }
        }

        metrics = builder.Build();
        return metrics.NumberOfChars > 0;
    }

    private static bool HasInlineSizeTextContentMetricBarriers(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs)
    {
        return HasSequentialTextRunBarriers(svgTextBase) ||
               ContainsInlineSizeTextContentMetricBarrier(svgTextBase, HasExplicitLineBreakRun(runs));
    }

    private static bool ContainsInlineSizeTextContentMetricBarrier(SvgTextBase svgTextBase, bool canIgnoreOwnTextLength)
    {
        if (HasOwnTextLengthAdjustment(svgTextBase) && !canIgnoreOwnTextLength)
        {
            return true;
        }

        foreach (var node in GetContentNodes(svgTextBase))
        {
            if (node is SvgTextBase childTextBase &&
                CanRenderTextSubtree(childTextBase) &&
                ContainsInlineSizeTextContentMetricBarrier(childTextBase, canIgnoreOwnTextLength: false))
            {
                return true;
            }
        }

        return false;
    }

    private static TextDomRunClusterMetric[] ClipTextDomRunClusters(
        TextDomRunClusterMetric[] clusters,
        SKRect clipRect)
    {
        if (clusters.Length == 0 || clipRect.IsEmpty)
        {
            return clusters;
        }

        var clipped = new TextDomRunClusterMetric[clusters.Length];
        for (var i = 0; i < clusters.Length; i++)
        {
            var cluster = clusters[i];
            var extent = cluster.Extent;
            var hitExtent = cluster.HitExtent;
            if (!extent.IsEmpty &&
                TryIntersectRect(extent, clipRect, out var clippedExtent))
            {
                extent = clippedExtent;
            }
            else if (!extent.IsEmpty)
            {
                extent = SKRect.Empty;
            }

            if (!hitExtent.IsEmpty &&
                TryIntersectRect(hitExtent, clipRect, out var clippedHitExtent))
            {
                hitExtent = clippedHitExtent;
            }
            else if (!hitExtent.IsEmpty)
            {
                hitExtent = SKRect.Empty;
            }

            clipped[i] = new TextDomRunClusterMetric(
                cluster.CharLength,
                cluster.StartOffset,
                cluster.EndOffset,
                cluster.StartPoint,
                cluster.EndPoint,
                extent,
                cluster.RotationDegrees)
            {
                HitExtent = hitExtent
            };
        }

        return clipped;
    }

    private static bool TryCreateSequentialTextContentMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SvgTextContentMetrics metrics)
    {
        metrics = SvgTextContentMetrics.Empty;
        if (HasSequentialTextRunBarriers(svgTextBase) ||
            !TryCollectSequentialTextContentRuns(svgTextBase, trimLeadingWhitespaceAtStart: true, out var runs))
        {
            return false;
        }

        var plainRuns = new List<SequentialTextRun>(runs.Count);
        for (var i = 0; i < runs.Count; i++)
        {
            plainRuns.Add(new SequentialTextRun(runs[i].StyleSource, runs[i].Text));
        }

        var currentX = 0f;
        var currentY = 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        currentX += baselineShift.X;
        currentY += baselineShift.Y;
        ApplyInitialChildContainerOffsets(svgTextBase, viewport, assetLoader, ref currentX, ref currentY);

        var totalAdvance = MeasureSequentialTextRuns(plainRuns, viewport, assetLoader);
        var isVertical = IsVerticalWritingMode(svgTextBase);
        if (isVertical && svgTextBase.X.Count > 0)
        {
            currentX -= baselineShift.X;
        }

        var textAlign = GetTextAnchorAlign(svgTextBase, viewport);
        var inlineOrigin = isVertical
            ? GetVerticalInlineStartCoordinate(svgTextBase, currentY, totalAdvance, textAlign)
            : GetAlignedStartCoordinate(currentX, totalAdvance, textAlign);
        var drawX = isVertical ? currentX : inlineOrigin;
        var drawY = isVertical ? inlineOrigin : currentY;
        var builder = new SvgTextContentMetricsBuilder();
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (!TryCreateTextRunMetrics(
                    run.StyleSource,
                    run.Text,
                    drawX,
                    drawY,
                    viewport,
                    assetLoader,
                    run.Rotations,
                    forceLeftAlign: true,
                    out var clusters,
                    out var runLength))
            {
                return false;
            }

            builder.AppendRun(clusters, runLength);
            ApplyInlineAdvance(run.StyleSource, ref drawX, ref drawY, runLength);
        }

        metrics = builder.Build();
        return true;
    }

    private static bool TryCreateBidiSequentialTextContentMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SvgTextContentMetrics metrics)
    {
        metrics = SvgTextContentMetrics.Empty;
        if (HasSequentialTextRunBarriers(svgTextBase) ||
            !TryCollectSequentialTextContentRuns(svgTextBase, trimLeadingWhitespaceAtStart: true, out var runs) ||
            runs.Count != 1 ||
            runs[0].Rotations is { Length: > 0 })
        {
            return false;
        }

        var run = runs[0];
        var paint = new SKPaint();
        PaintingService.SetPaintText(run.StyleSource, viewport, paint);
        if (!TryCreateBidiClusterSources(run.StyleSource, run.Text, viewport, paint, assetLoader, out var sources, out var totalAdvance))
        {
            return false;
        }

        var xs = new List<float>();
        var ys = new List<float>();
        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
        var currentX = xs.Count >= 1 ? xs[0] : 0f;
        var currentY = ys.Count >= 1 ? ys[0] : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        currentX += baselineShift.X;
        currentY += baselineShift.Y;
        ApplyInitialChildContainerOffsets(svgTextBase, viewport, assetLoader, ref currentX, ref currentY);

        var isVertical = IsVerticalWritingMode(svgTextBase);
        if (isVertical && svgTextBase.X.Count > 0)
        {
            currentX -= baselineShift.X;
        }

        var textAlign = GetTextAnchorAlign(svgTextBase, viewport);
        var inlineOrigin = isVertical
            ? GetVerticalInlineStartCoordinate(svgTextBase, currentY, totalAdvance, textAlign)
            : GetAlignedStartCoordinate(currentX, totalAdvance, textAlign);
        var drawX = isVertical ? currentX : inlineOrigin;
        var drawY = isVertical ? inlineOrigin : currentY;
        if (!TryCreateUnpositionedTextRunMetrics(
                run.StyleSource,
                viewport,
                assetLoader,
                paint,
                drawX,
                drawY,
                GetLogicalStartTextAlign(svgTextBase),
                sources,
                totalAdvance,
                out var clusters,
                out var runLength))
        {
            return false;
        }

        var builder = new SvgTextContentMetricsBuilder();
        builder.AppendRun(clusters, runLength);
        metrics = builder.Build();
        return metrics.NumberOfChars > 0;
    }

    private static bool TryCollectSequentialTextContentRuns(
        SvgTextBase svgTextBase,
        bool trimLeadingWhitespaceAtStart,
        out List<SequentialTextContentRun> runs)
    {
        runs = new List<SequentialTextContentRun>();
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        var rotationState = ResolveRotationState(svgTextBase, null);
        if (!TryCollectSequentialTextContentRuns(
                GetContentNodeList(svgTextBase),
                svgTextBase,
                runs,
                ref trimLeadingWhitespace,
                ref previousEndedWithSpace,
                rotationState))
        {
            return false;
        }

        return runs.Count > 0;
    }

    private static bool TryCollectSequentialTextContentRuns(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase styleSource,
        List<SequentialTextContentRun> runs,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace,
        RotationState? rotationState)
    {
        var contentNodeList = ToContentNodeList(contentNodes);
        for (var nodeIndex = 0; nodeIndex < contentNodeList.Count; nodeIndex++)
        {
            var node = contentNodeList[nodeIndex];
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor))
                    {
                        break;
                    }

                    if (!TryCollectSequentialTextContentRuns(
                            GetContentNodeList(svgAnchor),
                            CreateAnchorTextStyleSource(svgAnchor),
                            runs,
                            ref trimLeadingWhitespace,
                            ref previousEndedWithSpace,
                            rotationState))
                    {
                        return false;
                    }

                    break;

                case SvgTextSpan svgTextSpan:
                    if (!CanRenderTextSubtree(svgTextSpan))
                    {
                        break;
                    }

                    if (StartsPositionedTextChunk(svgTextSpan))
                    {
                        return false;
                    }

                    var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace;
                    var childPreviousEndedWithSpace = false;
                    var childRotationState = ResolveRotationState(svgTextSpan, rotationState);
                    var beforeChildRuns = runs.Count;
                    if (!TryCollectSequentialTextContentRuns(
                            GetContentNodeList(svgTextSpan),
                            svgTextSpan,
                            runs,
                            ref childTrimLeadingWhitespace,
                            ref childPreviousEndedWithSpace,
                            childRotationState))
                    {
                        return false;
                    }

                    AdvanceInheritedRotationState(rotationState, svgTextSpan, childTrimLeadingWhitespace);
                    if (runs.Count > beforeChildRuns || childPreviousEndedWithSpace)
                    {
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = childPreviousEndedWithSpace;
                    }

                    break;

                case SvgTextPath:
                case SvgTextRef:
                    return false;

                case not SvgTextBase:
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(
                        styleSource,
                        node.Content,
                        trimLeadingWhitespace: trimLeadingWhitespace,
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex));
                    if (!string.IsNullOrEmpty(text) &&
                        previousEndedWithSpace &&
                        CollapsesTextWhitespace(styleSource) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    runs.Add(new SequentialTextContentRun(styleSource, text!, ConsumeRotations(rotationState, text!)));
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                    break;
            }
        }

        return true;
    }

    private static bool TryCollectTextContentMetricsBase(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        RotationState? inheritedRotationState,
        AbsolutePositionState? inheritedAbsolutePositionState,
        bool trimLeadingWhitespaceAtStart,
        SvgTextContentMetricsBuilder builder)
    {
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        var localCurrentX = currentX + baselineShift.X;
        var localCurrentY = currentY + baselineShift.Y;
        var rotationState = ResolveRotationState(svgTextBase, inheritedRotationState);
        var absolutePositionState = ResolveAbsolutePositionState(svgTextBase, inheritedAbsolutePositionState, viewport);
        var useInitialPosition = true;
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        var success = TryCollectTextContentMetricsNodes(
            GetContentNodeList(svgTextBase),
            svgTextBase,
            ref localCurrentX,
            ref localCurrentY,
            ref useInitialPosition,
            ref trimLeadingWhitespace,
            ref previousEndedWithSpace,
            viewport,
            assetLoader,
            rotationState,
            absolutePositionState,
            builder);
        currentX = localCurrentX - baselineShift.X;
        currentY = localCurrentY - baselineShift.Y;
        return success;
    }

    private static bool TryCollectTextContentMetricsNodes(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        ref bool useInitialPosition,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        RotationState? rotationState,
        AbsolutePositionState? absolutePositionState,
        SvgTextContentMetricsBuilder builder)
    {
        var contentNodeList = ToContentNodeList(contentNodes);
        for (var nodeIndex = 0; nodeIndex < contentNodeList.Count; nodeIndex++)
        {
            var node = contentNodeList[nodeIndex];
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor))
                    {
                        break;
                    }

                    var anchorStyleSource = CreateAnchorTextStyleSource(svgAnchor);
                    if (!TryCollectTextContentMetricsNodes(
                            GetContentNodeList(svgAnchor),
                            anchorStyleSource,
                            ref currentX,
                            ref currentY,
                            ref useInitialPosition,
                            ref trimLeadingWhitespace,
                            ref previousEndedWithSpace,
                            viewport,
                            assetLoader,
                            rotationState,
                            absolutePositionState,
                            builder))
                    {
                        return false;
                    }

                    break;

                case not SvgTextBase:
                    var rawContent = node.Content;
                    if (string.IsNullOrEmpty(rawContent))
                    {
                        break;
                    }

                    var text = PrepareText(
                        svgTextBase,
                        rawContent,
                        trimLeadingWhitespace: trimLeadingWhitespace,
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex));
                    if (previousEndedWithSpace &&
                        CollapsesTextWhitespace(svgTextBase) &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text) &&
                        !string.IsNullOrWhiteSpace(rawContent) &&
                        CollapsesTextWhitespace(svgTextBase) &&
                        !previousEndedWithSpace &&
                        HasRenderableTextContentBefore(contentNodeList, nodeIndex) &&
                        HasRenderableTextContentAfter(contentNodeList, nodeIndex))
                    {
                        text = " ";
                    }

                    if (string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    var codepointCount = CountCodepoints(text!);
                    var xs = new List<float>();
                    var ys = new List<float>();
                    var dxs = new List<float>();
                    var dys = new List<float>();
                    absolutePositionState?.BuildEffectiveAbsolutePositions(codepointCount, xs, ys);
                    if (absolutePositionState is null)
                    {
                        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
                        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
                    }

                    GetPositionsDX(svgTextBase, viewport, assetLoader, dxs);
                    GetPositionsDY(svgTextBase, viewport, assetLoader, dys);
                    var rotations = ConsumeRotations(rotationState, text!);

                    if (useInitialPosition &&
                        TryCreatePositionedCodepointPoints(svgTextBase, text!, xs, ys, dxs, dys, currentX, currentY, viewport, assetLoader, rotations, out var positionedPoints))
                    {
                        var placements = CreatePositionedCodepointPlacements(svgTextBase, text!, positionedPoints, rotations);
                        if (!TryCreatePlacedTextRunMetrics(svgTextBase, text!, viewport, assetLoader, placements, out var positionedClusters, out var positionedLength))
                        {
                            return false;
                        }

                        builder.AppendRun(positionedClusters, positionedLength);
                        var lastClusterAdvance = positionedClusters.Length > 0
                            ? positionedClusters[positionedClusters.Length - 1].EndOffset - positionedClusters[positionedClusters.Length - 1].StartOffset
                            : 0f;
                        MoveToAfterPositionedRun(
                            svgTextBase,
                            positionedPoints[positionedPoints.Length - 1],
                            lastClusterAdvance,
                            out currentX,
                            out currentY);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(codepointCount);
                        break;
                    }

                    var resetX = useInitialPosition && xs.Count >= 1;
                    var resetY = useInitialPosition && ys.Count >= 1;
                    var x = resetX ? xs[0] : currentX;
                    var y = resetY ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;
                    ApplyBaselineShiftToResetInitialAxes(svgTextBase, viewport, assetLoader, resetX, resetY, ref currentX, ref currentY);

                    if (!TryCreateTextRunMetrics(svgTextBase, text!, currentX, currentY, viewport, assetLoader, rotations, false, out var runClusters, out var runLength))
                    {
                        return false;
                    }

                    builder.AppendRun(runClusters, runLength);
                    ApplyInlineAdvance(svgTextBase, ref currentX, ref currentY, runLength);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                    absolutePositionState?.Consume(codepointCount);
                    break;

                case SvgTextRef svgTextRef:
                    {
                        if (ShouldSuppressInlineTextReferenceContent(contentNodeList, nodeIndex))
                        {
                            break;
                        }

                        if (!CanRenderTextSubtree(svgTextRef) ||
                            !IsTextReferenceRenderingEnabled(assetLoader) ||
                            SvgService.HasRecursiveReference(svgTextRef, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedElement), new HashSet<Uri>()) ||
                            !TryResolveTextReferenceContent(svgTextRef, out var rawReferencedText))
                        {
                            break;
                        }

                        var referencedText = PrepareResolvedContent(svgTextRef, rawReferencedText!, trimLeadingWhitespace, previousEndedWithSpace);
                        if (string.IsNullOrEmpty(referencedText))
                        {
                            break;
                        }

                        var referencedTextValue = referencedText!;
                        var referencedCodepointCount = CountCodepoints(referencedTextValue);
                        var referencedXs = new List<float>();
                        var referencedYs = new List<float>();
                        var referencedDxs = new List<float>();
                        var referencedDys = new List<float>();
                        absolutePositionState?.BuildEffectiveAbsolutePositions(referencedCodepointCount, referencedXs, referencedYs);
                        if (absolutePositionState is null)
                        {
                            GetPositionsX(svgTextRef, viewport, assetLoader, referencedXs);
                            GetPositionsY(svgTextRef, viewport, assetLoader, referencedYs);
                        }

                        GetPositionsDX(svgTextRef, viewport, assetLoader, referencedDxs);
                        GetPositionsDY(svgTextRef, viewport, assetLoader, referencedDys);
                        var referencedRotations = ConsumeRotations(rotationState, referencedTextValue);

                        if (useInitialPosition &&
                            TryCreatePositionedCodepointPoints(svgTextRef, referencedTextValue, referencedXs, referencedYs, referencedDxs, referencedDys, currentX, currentY, viewport, assetLoader, referencedRotations, out var referencedPoints))
                        {
                            var referencedPlacements = CreatePositionedCodepointPlacements(svgTextRef, referencedTextValue, referencedPoints, referencedRotations);
                            if (!TryCreatePlacedTextRunMetrics(svgTextRef, referencedTextValue, viewport, assetLoader, referencedPlacements, out var referencedPositionedClusters, out var referencedPositionedLength))
                            {
                                return false;
                            }

                            builder.AppendRun(referencedPositionedClusters, referencedPositionedLength);
                            var referencedLastClusterAdvance = referencedPositionedClusters.Length > 0
                                ? referencedPositionedClusters[referencedPositionedClusters.Length - 1].EndOffset - referencedPositionedClusters[referencedPositionedClusters.Length - 1].StartOffset
                                : 0f;
                            MoveToAfterPositionedRun(
                                svgTextRef,
                                referencedPoints[referencedPoints.Length - 1],
                                referencedLastClusterAdvance,
                                out currentX,
                                out currentY);
                            useInitialPosition = false;
                            trimLeadingWhitespace = false;
                            previousEndedWithSpace = referencedTextValue.EndsWith(" ", StringComparison.Ordinal);
                            absolutePositionState?.Consume(referencedCodepointCount);
                            break;
                        }

                        var referencedResetX = useInitialPosition && referencedXs.Count >= 1;
                        var referencedResetY = useInitialPosition && referencedYs.Count >= 1;
                        var referencedX = referencedResetX ? referencedXs[0] : currentX;
                        var referencedY = referencedResetY ? referencedYs[0] : currentY;
                        var referencedDx = useInitialPosition && referencedDxs.Count >= 1 ? referencedDxs[0] : 0f;
                        var referencedDy = useInitialPosition && referencedDys.Count >= 1 ? referencedDys[0] : 0f;
                        currentX = referencedX + referencedDx;
                        currentY = referencedY + referencedDy;
                        ApplyBaselineShiftToResetInitialAxes(svgTextRef, viewport, assetLoader, referencedResetX, referencedResetY, ref currentX, ref currentY);

                        if (!TryCreateTextRunMetrics(svgTextRef, referencedTextValue, currentX, currentY, viewport, assetLoader, referencedRotations, false, out var referencedClusters, out var referencedLength))
                        {
                            return false;
                        }

                        builder.AppendRun(referencedClusters, referencedLength);
                        ApplyInlineAdvance(svgTextRef, ref currentX, ref currentY, referencedLength);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = referencedTextValue.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(referencedCodepointCount);
                        break;
                    }

                case SvgTextPath:
                    return false;

                case SvgTextSpan svgTextSpan:
                    if (!CanRenderTextSubtree(svgTextSpan))
                    {
                        break;
                    }

                    var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace || StartsPositionedTextChunk(svgTextSpan);
                    if (!TryCollectTextContentMetricsBase(
                            svgTextSpan,
                            ref currentX,
                            ref currentY,
                            viewport,
                            assetLoader,
                            rotationState,
                            absolutePositionState,
                            childTrimLeadingWhitespace,
                            builder))
                    {
                        return false;
                    }

                    AdvanceInheritedAbsolutePositionState(absolutePositionState, svgTextSpan, childTrimLeadingWhitespace);
                    AdvanceInheritedRotationState(rotationState, svgTextSpan, childTrimLeadingWhitespace);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = EndsWithCollapsedSpace(svgTextSpan);
                    break;
            }
        }

        return true;
    }

    private static bool TryCreateTextRunMetrics(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        float[]? rotations,
        bool forceLeftAlign,
        out TextDomRunClusterMetric[] clusters,
        out float runLength)
    {
        clusters = Array.Empty<TextDomRunClusterMetric>();
        runLength = 0f;
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        var textAlign = forceLeftAlign ? GetLogicalStartTextAlign(svgTextBase) : GetTextAnchorAlign(svgTextBase, viewport);

        if (TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, viewport, textAlign, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            return TryCreateVerticalTextRunMetrics(svgTextBase, verticalPlacements, verticalAdvance, viewport, assetLoader, out clusters, out runLength);
        }

        if (TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, viewport, textAlign, assetLoader, rotations, out var placements, out _, out var placementCodepoints, out var naturalAdvances))
        {
            return TryCreatePlacedTextRunMetrics(
                svgTextBase,
                text,
                viewport,
                assetLoader,
                placements,
                placements.Length > 0 ? placements[0].Point.X : 0f,
                out clusters,
                out runLength,
                placementCodepoints,
                naturalAdvances);
        }

        if (TryCreateSimpleUnpositionedTextRunMetrics(
                svgTextBase,
                text,
                viewport,
                assetLoader,
                paint,
                anchorX,
                anchorY,
                textAlign,
                out clusters,
                out runLength))
        {
            return true;
        }

        if (rotations is not { Length: > 0 } &&
            TryCreateBidiClusterSources(svgTextBase, text, viewport, paint, assetLoader, out var bidiClusters, out var bidiAdvance))
        {
            return TryCreateUnpositionedTextRunMetrics(svgTextBase, viewport, assetLoader, paint, anchorX, anchorY, textAlign, bidiClusters, bidiAdvance, out clusters, out runLength);
        }

        if (TryCreateSvgFontTextRunMetrics(svgTextBase, text, paint, anchorX, anchorY, textAlign, assetLoader, out clusters, out runLength))
        {
            return true;
        }

        if (TryCreateSvgFontClusterSources(svgTextBase, text, paint, assetLoader, out var svgFontClusters, out var svgFontAdvance))
        {
            return TryCreateUnpositionedTextRunMetrics(svgTextBase, viewport, assetLoader, paint, anchorX, anchorY, textAlign, svgFontClusters, svgFontAdvance, out clusters, out runLength);
        }

        if (TryCreateShapedClusterSources(svgTextBase, text, viewport, paint, assetLoader, out var shapedClusters, out var shapedAdvance))
        {
            return TryCreateUnpositionedTextRunMetrics(svgTextBase, viewport, assetLoader, paint, anchorX, anchorY, textAlign, shapedClusters, shapedAdvance, out clusters, out runLength);
        }

        return TryCreateUnpositionedTextRunMetrics(
            svgTextBase,
            viewport,
            assetLoader,
            paint,
            anchorX,
            anchorY,
            textAlign,
            CreateFallbackClusterSources(svgTextBase, text, viewport, assetLoader, out var fallbackAdvance),
            fallbackAdvance,
            out clusters,
            out runLength);
    }

    private static bool TryCreateVerticalTextRunMetrics(
        SvgTextBase svgTextBase,
        IReadOnlyList<VerticalTextRunPlacement> placements,
        float totalAdvance,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out TextDomRunClusterMetric[] clusters,
        out float runLength)
    {
        clusters = Array.Empty<TextDomRunClusterMetric>();
        runLength = 0f;
        if (placements.Count == 0)
        {
            return false;
        }

        var runClusters = new TextDomRunClusterMetric[placements.Count];
        var currentOffset = 0f;
        var inlineDirection = GetInlineAdvanceDirection(svgTextBase);
        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var extent = MeasureVerticalTextRunPlacementsBounds(svgTextBase, new[] { placement }, viewport, assetLoader, out _);
            runClusters[i] = new TextDomRunClusterMetric(
                placement.Text.Length,
                currentOffset,
                currentOffset + placement.Advance,
                placement.Placement.Point,
                new SKPoint(placement.Placement.Point.X, placement.Placement.Point.Y + (placement.Advance * inlineDirection)),
                extent,
                placement.Placement.RotationDegrees);
            currentOffset += placement.Advance;
        }

        clusters = runClusters;
        runLength = totalAdvance;
        return true;
    }

    private static bool TryCreatePlacedTextRunMetrics(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        PositionedCodepointPlacement[] placements,
        out TextDomRunClusterMetric[] clusters,
        out float runLength)
    {
        return TryCreatePlacedTextRunMetrics(
            svgTextBase,
            text,
            viewport,
            assetLoader,
            placements,
            placements.Length > 0 ? placements[0].Point.X : 0f,
            out clusters,
            out runLength);
    }

    private static bool TryCreatePlacedTextRunMetrics(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        PositionedCodepointPlacement[] placements,
        float originX,
        out TextDomRunClusterMetric[] clusters,
        out float runLength,
        IReadOnlyList<string>? knownCodepoints = null,
        float[]? knownNaturalAdvances = null)
    {
        clusters = Array.Empty<TextDomRunClusterMetric>();
        runLength = 0f;
        if (placements.Length == 0 || IsVerticalWritingMode(svgTextBase))
        {
            return false;
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        if (TryCreateSimpleSpacingPlacedTextRunMetrics(
                svgTextBase,
                text,
                viewport,
                assetLoader,
                paint,
                placements,
                originX,
                out clusters,
                out runLength,
                knownCodepoints,
                knownNaturalAdvances))
        {
            return true;
        }

        if (!TryCreateClusterSources(svgTextBase, text, viewport, paint, assetLoader, out var sources, out _, out var sourcesUseSvgFont))
        {
            return false;
        }

        var runClusters = new List<TextDomRunClusterMetric>(sources.Length);
        var includeWhitespaceMetrics = PreservesTextDomWhitespace(svgTextBase);
        var maxEndOffset = 0f;
        for (var i = 0; i < sources.Length; i++)
        {
            var source = sources[i];
            if (source.FirstCodepointIndex < 0 || source.FirstCodepointIndex >= placements.Length)
            {
                return false;
            }

            var placement = placements[source.FirstCodepointIndex];
            var extentPlacement = placement with { RotationDegrees = 0f };
            var startOffset = placement.Point.X - originX;
            var endOffset = startOffset + (source.Advance * placement.ScaleX);
            var extent = SKRect.Empty;
            var hitExtent = SKRect.Empty;
            if (sourcesUseSvgFont)
            {
                _ = TryMeasureTextDomClusterBounds(
                    svgTextBase,
                    source.Text,
                    extentPlacement,
                    paint,
                    assetLoader,
                    out extent,
                    out hitExtent,
                    out _);
            }
            else
            {
                extent = CreateFallbackTextDomClusterBounds(
                    svgTextBase,
                    extentPlacement.Point,
                    source.Advance,
                    extentPlacement.ScaleX,
                    GetScalePivot(extentPlacement),
                    extentPlacement.RotationDegrees,
                    paint,
                    assetLoader,
                    source.Text,
                    out hitExtent);
            }

            var startPoint = placement.Point;
            var endPoint = new SKPoint(placement.Point.X + (source.Advance * placement.ScaleX), placement.Point.Y);
            var isWhitespace = IsWhitespaceOnlyText(source.Text);
            runClusters.Add(new TextDomRunClusterMetric(
                includeWhitespaceMetrics || !isWhitespace ? source.Text.Length : 0,
                startOffset,
                endOffset,
                startPoint,
                endPoint,
                extent,
                placement.RotationDegrees)
            {
                HitExtent = hitExtent
            });

            maxEndOffset = Math.Max(maxEndOffset, endOffset);
        }

        clusters = MergeTextDomRunClustersByGraphemeClusters(text, runClusters);
        runLength = maxEndOffset;
        return true;
    }

    private static bool TryCreateSimpleSpacingPlacedTextRunMetrics(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKPaint paint,
        PositionedCodepointPlacement[] placements,
        float originX,
        out TextDomRunClusterMetric[] clusters,
        out float runLength,
        IReadOnlyList<string>? knownCodepoints = null,
        float[]? knownNaturalAdvances = null)
    {
        clusters = Array.Empty<TextDomRunClusterMetric>();
        runLength = 0f;
        if (placements.Length == 0 ||
            HasOwnTextLengthAdjustment(svgTextBase) ||
            IsRightToLeft(svgTextBase) ||
            SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase) != SvgUnicodeBidiMode.Normal ||
            RequiresSyntheticSmallCaps(svgTextBase, text) ||
            !IsSimpleAsciiSequentialCompileText(text))
        {
            return false;
        }

        for (var i = 0; i < placements.Length; i++)
        {
            if (placements[i].RotationDegrees != 0f ||
                placements[i].ScaleX != 1f)
            {
                return false;
            }
        }

        if (TryCreateSvgFontClusterSources(svgTextBase, text, paint, assetLoader, out _, out _))
        {
            return false;
        }

        var codepoints = knownCodepoints ?? SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0 ||
            codepoints.Count != placements.Length ||
            !HasEffectiveSpacingAdjustments(svgTextBase, codepoints))
        {
            return false;
        }

        var advances = knownNaturalAdvances ?? MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, viewport, assetLoader);
        if (advances.Length != codepoints.Count)
        {
            return false;
        }

        var includeWhitespaceMetrics = PreservesTextDomWhitespace(svgTextBase);
        var runClusters = new TextDomRunClusterMetric[codepoints.Count];
        var maxEndOffset = 0f;
        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i];
            var placement = placements[i];
            var startOffset = placement.Point.X - originX;
            var advance = advances[i];
            var endOffset = startOffset + advance;
            var extent = CreateFallbackTextDomClusterBounds(
                svgTextBase,
                placement.Point,
                advance,
                placement.ScaleX,
                GetScalePivot(placement),
                placement.RotationDegrees,
                paint,
                assetLoader,
                codepoint,
                out var hitExtent);
            var isWhitespace = IsWhitespaceOnlyText(codepoint);
            runClusters[i] = new TextDomRunClusterMetric(
                includeWhitespaceMetrics || !isWhitespace ? codepoint.Length : 0,
                startOffset,
                endOffset,
                placement.Point,
                new SKPoint(placement.Point.X + advance, placement.Point.Y),
                extent,
                placement.RotationDegrees)
            {
                HitExtent = hitExtent
            };

            maxEndOffset = Math.Max(maxEndOffset, endOffset);
        }

        clusters = runClusters;
        runLength = maxEndOffset;
        return true;
    }

    private static bool TryCreateSimpleUnpositionedTextRunMetrics(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKPaint paint,
        float anchorX,
        float anchorY,
        SKTextAlign textAlign,
        out TextDomRunClusterMetric[] clusters,
        out float runLength)
    {
        clusters = Array.Empty<TextDomRunClusterMetric>();
        runLength = 0f;
        if (HasOwnTextLengthAdjustment(svgTextBase) ||
            IsVerticalWritingMode(svgTextBase) ||
            IsRightToLeft(svgTextBase) ||
            SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase) != SvgUnicodeBidiMode.Normal ||
            RequiresSyntheticSmallCaps(svgTextBase, text) ||
            !IsSimpleAsciiSequentialCompileText(text))
        {
            return false;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0 ||
            HasEffectiveSpacingAdjustments(svgTextBase, codepoints))
        {
            return false;
        }

        if (TryCreateSvgFontClusterSources(svgTextBase, text, paint, assetLoader, out _, out _))
        {
            return false;
        }

        var advances = MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, viewport, assetLoader);
        if (advances.Length != codepoints.Count)
        {
            return false;
        }

        var totalAdvance = 0f;
        for (var i = 0; i < advances.Length; i++)
        {
            totalAdvance += advances[i];
        }

        var startX = GetAlignedStartCoordinate(anchorX, totalAdvance, textAlign);
        var runClusters = new TextDomRunClusterMetric[codepoints.Count];
        var relativeOffset = 0f;
        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i];
            var advance = advances[i];
            var point = new SKPoint(startX + relativeOffset, anchorY);
            var extent = CreateFallbackTextDomClusterBounds(
                svgTextBase,
                point,
                advance,
                1f,
                point,
                0f,
                paint,
                assetLoader,
                codepoint,
                out var hitExtent);
            runClusters[i] = new TextDomRunClusterMetric(
                codepoint.Length,
                relativeOffset,
                relativeOffset + advance,
                point,
                new SKPoint(point.X + advance, point.Y),
                extent,
                0f)
            {
                HitExtent = hitExtent
            };
            relativeOffset += advance;
        }

        clusters = runClusters;
        runLength = totalAdvance;
        return true;
    }

    private static bool TryCreateTextPathRunMetrics(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        PositionedCodepointPlacement[] placements,
        out TextDomRunClusterMetric[] clusters,
        out float runLength)
    {
        clusters = Array.Empty<TextDomRunClusterMetric>();
        runLength = 0f;
        if (placements.Length == 0 || IsVerticalWritingMode(svgTextBase))
        {
            return false;
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        if (!TryCreateClusterSources(svgTextBase, text, viewport, paint, assetLoader, out var sources, out var totalAdvance, out var sourcesUseSvgFont))
        {
            return false;
        }

        var runClusters = new List<TextDomRunClusterMetric>(sources.Length);
        var includeWhitespaceMetrics = PreservesTextDomWhitespace(svgTextBase);
        var originInlineOffset = placements[0].InlineOffset;
        var hasInlineOffsets = !float.IsNaN(originInlineOffset);
        var maxEndOffset = 0f;
        var previousStartOffset = float.NegativeInfinity;
        var previousEndPoint = default(SKPoint);
        for (var i = 0; i < sources.Length; i++)
        {
            var source = sources[i];
            if (source.FirstCodepointIndex < 0 || source.FirstCodepointIndex >= placements.Length)
            {
                return false;
            }

            var placement = placements[source.FirstCodepointIndex];
            var advance = source.Advance * placement.ScaleX;
            var startOffset = hasInlineOffsets && !float.IsNaN(placement.InlineOffset)
                ? placement.InlineOffset - originInlineOffset
                : source.RelativeOffset;
            var rotationRadians = placement.RotationDegrees * (float)Math.PI / 180f;
            var startPoint = placement.Point;
            var orderingOffset = Math.Max(startOffset, source.RelativeOffset);
            if (orderingOffset > previousStartOffset + TextLengthTolerance &&
                runClusters.Count > 0 &&
                Math.Abs(startPoint.X - runClusters[runClusters.Count - 1].StartPoint.X) <= TextLengthTolerance &&
                Math.Abs(startPoint.Y - runClusters[runClusters.Count - 1].StartPoint.Y) <= TextLengthTolerance)
            {
                startPoint = previousEndPoint;
            }

            var adjustedPlacement = placement with { Point = startPoint };
            var endPoint = new SKPoint(
                startPoint.X + ((float)Math.Cos(rotationRadians) * advance),
                startPoint.Y + ((float)Math.Sin(rotationRadians) * advance));
            var extent = SKRect.Empty;
            var hitExtent = SKRect.Empty;
            if (sourcesUseSvgFont)
            {
                _ = TryMeasureTextDomClusterBounds(
                    svgTextBase,
                    source.Text,
                    adjustedPlacement,
                    paint,
                    assetLoader,
                    out extent,
                    out hitExtent,
                    out _);
            }
            else
            {
                extent = CreateFallbackTextDomClusterBounds(
                    svgTextBase,
                    startPoint,
                    source.Advance,
                    placement.ScaleX,
                    GetScalePivot(adjustedPlacement),
                    placement.RotationDegrees,
                    paint,
                    assetLoader,
                    source.Text,
                    out hitExtent);
            }

            var isWhitespace = IsWhitespaceOnlyText(source.Text);
            runClusters.Add(new TextDomRunClusterMetric(
                includeWhitespaceMetrics || !isWhitespace ? source.Text.Length : 0,
                startOffset,
                startOffset + advance,
                startPoint,
                endPoint,
                extent,
                placement.RotationDegrees)
            {
                HitExtent = hitExtent
            });

            maxEndOffset = Math.Max(maxEndOffset, startOffset + advance);
            previousStartOffset = startOffset;
            previousEndPoint = endPoint;
        }

        NormalizeTextPathClusterPoints(runClusters);
        clusters = MergeTextDomRunClustersByGraphemeClusters(text, runClusters);
        runLength = hasInlineOffsets ? maxEndOffset : totalAdvance;
        return true;
    }

    private static TextDomRunClusterMetric[] MergeTextDomRunClustersByGraphemeClusters(
        string text,
        IReadOnlyList<TextDomRunClusterMetric> clusters)
    {
        if (string.IsNullOrEmpty(text) || clusters.Count <= 1)
        {
            return CopyTextDomRunClusters(clusters);
        }

        var clusterStarts = SvgTextBoundaryResolver.Default.GetGraphemeClusterStartCharIndexes(text);
        if (clusterStarts.Count <= 1 || clusterStarts.Count >= clusters.Count)
        {
            return CopyTextDomRunClusters(clusters);
        }

        var mergedClusters = new List<TextDomRunClusterMetric>(clusterStarts.Count);
        var sourceIndex = 0;
        var sourceCharIndex = 0;
        for (var clusterIndex = 0; clusterIndex < clusterStarts.Count; clusterIndex++)
        {
            var clusterStart = clusterStarts[clusterIndex];
            var clusterEnd = clusterIndex + 1 < clusterStarts.Count
                ? clusterStarts[clusterIndex + 1]
                : text.Length;
            if (clusterStart != sourceCharIndex ||
                clusterEnd <= clusterStart ||
                sourceIndex >= clusters.Count)
            {
                return CopyTextDomRunClusters(clusters);
            }

            var first = clusters[sourceIndex];
            var startOffset = first.StartOffset;
            var endOffset = first.EndOffset;
            var startPoint = first.StartPoint;
            var endPoint = first.EndPoint;
            var extent = first.Extent;
            var hitExtent = first.HitExtent;
            while (sourceIndex < clusters.Count && sourceCharIndex < clusterEnd)
            {
                var source = clusters[sourceIndex];
                if (source.CharLength <= 0)
                {
                    return CopyTextDomRunClusters(clusters);
                }

                endOffset = Math.Max(endOffset, source.EndOffset);
                endPoint = source.EndPoint;
                UnionBounds(ref extent, source.Extent);
                UnionBounds(ref hitExtent, source.HitExtent);
                sourceCharIndex += source.CharLength;
                sourceIndex++;
            }

            if (sourceCharIndex != clusterEnd)
            {
                return CopyTextDomRunClusters(clusters);
            }

            mergedClusters.Add(new TextDomRunClusterMetric(
                clusterEnd - clusterStart,
                startOffset,
                endOffset,
                startPoint,
                endPoint,
                extent,
                first.RotationDegrees)
            {
                HitExtent = hitExtent
            });
        }

        return sourceIndex == clusters.Count && sourceCharIndex == text.Length
            ? mergedClusters.ToArray()
            : CopyTextDomRunClusters(clusters);
    }

    private static TextDomRunClusterMetric[] CopyTextDomRunClusters(IReadOnlyList<TextDomRunClusterMetric> clusters)
    {
        if (clusters is TextDomRunClusterMetric[] array)
        {
            return array;
        }

        var copy = new TextDomRunClusterMetric[clusters.Count];
        for (var i = 0; i < clusters.Count; i++)
        {
            copy[i] = clusters[i];
        }

        return copy;
    }

    private static void NormalizeTextPathClusterPoints(List<TextDomRunClusterMetric> runClusters)
    {
        for (var i = 1; i < runClusters.Count; i++)
        {
            var previous = runClusters[i - 1];
            var current = runClusters[i];
            if (Math.Abs(current.StartPoint.X - previous.StartPoint.X) > TextLengthTolerance ||
                Math.Abs(current.StartPoint.Y - previous.StartPoint.Y) > TextLengthTolerance)
            {
                continue;
            }

            var rotationRadians = current.RotationDegrees * (float)Math.PI / 180f;
            var advance = Math.Max(0f, current.EndOffset - current.StartOffset);
            var startPoint = previous.EndPoint;
            var endPoint = new SKPoint(
                startPoint.X + ((float)Math.Cos(rotationRadians) * advance),
                startPoint.Y + ((float)Math.Sin(rotationRadians) * advance));
            var extent = current.Extent.IsEmpty
                ? current.Extent
                : new SKRect(
                    Math.Min(startPoint.X, endPoint.X),
                    current.Extent.Top,
                    Math.Max(startPoint.X, endPoint.X),
                    current.Extent.Bottom);
            var hitExtent = current.HitExtent.IsEmpty
                ? current.HitExtent
                : new SKRect(
                    Math.Min(startPoint.X, endPoint.X),
                    current.HitExtent.Top,
                    Math.Max(startPoint.X, endPoint.X),
                    current.HitExtent.Bottom);
            runClusters[i] = current with
            {
                StartPoint = startPoint,
                EndPoint = endPoint,
                Extent = extent,
                HitExtent = hitExtent
            };
        }
    }

    private static bool PreservesTextDomWhitespace(SvgTextBase svgTextBase)
    {
        return GetInlineSizeWhiteSpaceModel(svgTextBase).PreservesLineEdgeWhitespace;
    }

    private static bool TryCreateUnpositionedTextRunMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKPaint paint,
        float anchorX,
        float anchorY,
        SKTextAlign textAlign,
        IReadOnlyList<TextDomClusterSource> sources,
        float totalAdvance,
        out TextDomRunClusterMetric[] clusters,
        out float runLength)
    {
        clusters = Array.Empty<TextDomRunClusterMetric>();
        runLength = 0f;
        if (sources.Count == 0 || IsVerticalWritingMode(svgTextBase))
        {
            return false;
        }

        var startX = GetAlignedStartCoordinate(anchorX, totalAdvance, textAlign);
        var runClusters = new TextDomRunClusterMetric[sources.Count];
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            var placement = new PositionedCodepointPlacement(
                new SKPoint(startX + source.RelativeOffset, anchorY),
                0f,
                1f,
                startX + source.RelativeOffset);
            if (!TryMeasureTextDomClusterBounds(svgTextBase, source.Text, placement, paint, assetLoader, out var extent, out var hitExtent, out _))
            {
                extent = SKRect.Empty;
                hitExtent = SKRect.Empty;
            }

            runClusters[i] = new TextDomRunClusterMetric(
                source.Text.Length,
                source.RelativeOffset,
                source.RelativeOffset + source.Advance,
                placement.Point,
                new SKPoint(placement.Point.X + source.Advance, placement.Point.Y),
                extent,
                0f)
            {
                HitExtent = hitExtent,
                StartCharIndex = source.FirstCharIndex
            };
        }

        clusters = runClusters;
        runLength = totalAdvance;
        return true;
    }

    private static bool TryCreateSvgFontTextRunMetrics(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        float anchorX,
        float anchorY,
        SKTextAlign textAlign,
        ISvgAssetLoader assetLoader,
        out TextDomRunClusterMetric[] clusters,
        out float runLength)
    {
        clusters = Array.Empty<TextDomRunClusterMetric>();
        runLength = 0f;
        if (IsVerticalWritingMode(svgTextBase) ||
            !SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, includeGlyphTexts: true, out var svgFontLayout) ||
            svgFontLayout is null)
        {
            return false;
        }

        var placements = svgFontLayout.GlyphPlacements;
        var placementTexts = svgFontLayout.GlyphTexts;
        if (placements.Count == 0 || placementTexts is null || placementTexts.Count != placements.Count)
        {
            return false;
        }

        var startX = GetAlignedStartCoordinate(anchorX, svgFontLayout.Advance, textAlign);
        var runClusters = new List<TextDomRunClusterMetric>(placements.Count);
        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var clusterText = placementTexts[i];
            if (clusterText.Length == 0)
            {
                continue;
            }

            var nextRelativeOffset = GetNextSvgFontClusterRelativeOffset(placements, placementTexts, i + 1, svgFontLayout.Advance);
            var advance = Math.Max(0f, nextRelativeOffset - placement.RelativeX);
            var point = new SKPoint(startX + placement.RelativeX, anchorY);
            var bounds = placement.RelativeBounds;
            var glyphExtent = bounds.IsEmpty
                ? SKRect.Empty
                : new SKRect(
                    bounds.Left + startX,
                    bounds.Top + anchorY,
                    bounds.Right + startX,
                    bounds.Bottom + anchorY);
            var hitExtent = IsWhitespaceOnlyText(clusterText) || glyphExtent.IsEmpty
                ? GetTextAdvanceBox(svgTextBase, point.X, point.Y, advance, paint, assetLoader)
                : glyphExtent;
            var extent = glyphExtent;
            extent = ExpandTextBoundsWithAdvanceBox(svgTextBase, extent, point.X, point.Y, advance, paint, assetLoader);

            runClusters.Add(new TextDomRunClusterMetric(
                clusterText.Length,
                placement.RelativeX,
                placement.RelativeX + advance,
                point,
                new SKPoint(point.X + advance, point.Y),
                extent,
                0f)
            {
                HitExtent = hitExtent
            });
        }

        clusters = runClusters.ToArray();
        runLength = svgFontLayout.Advance;
        return true;
    }

    private static bool TryCreateClusterSources(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out TextDomClusterSource[] sources,
        out float totalAdvance,
        out bool usesSvgFont)
    {
        if (TryCreateSvgFontClusterSources(svgTextBase, text, paint, assetLoader, out sources, out totalAdvance))
        {
            usesSvgFont = true;
            return true;
        }

        if (TryCreateShapedClusterSources(svgTextBase, text, viewport, paint, assetLoader, out sources, out totalAdvance))
        {
            usesSvgFont = false;
            return true;
        }

        sources = CreateFallbackClusterSources(svgTextBase, text, viewport, assetLoader, out totalAdvance);
        usesSvgFont = false;
        return sources.Length > 0;
    }

    private static bool TryCreateBidiClusterSources(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out TextDomClusterSource[] sources,
        out float totalAdvance)
    {
        sources = Array.Empty<TextDomClusterSource>();
        totalAdvance = 0f;
        if (string.IsNullOrEmpty(text) ||
            IsVerticalWritingMode(svgTextBase) ||
            HasOwnTextLengthAdjustment(svgTextBase))
        {
            return false;
        }

        var baseDirection = IsRightToLeft(svgTextBase) ? SvgTextDirection.RightToLeft : SvgTextDirection.LeftToRight;
        var mode = SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase);
        var visualRuns = SvgTextBidiResolver.CreateVisualRuns(text, baseDirection, mode);
        if (visualRuns.Count <= 1)
        {
            return false;
        }

        var preserveLogicalRtlPlacement = mode == SvgUnicodeBidiMode.PlainText &&
                                          ResolvePlainTextDirection(text, baseDirection) == SvgTextDirection.RightToLeft &&
                                          !HasPlainTextParagraphSeparator(text);
        if (preserveLogicalRtlPlacement)
        {
            visualRuns = visualRuns.OrderBy(static run => run.StartCharIndex).ToList();
        }

        var codepoints = SplitCodepoints(text);
        if (codepoints.Count == 0)
        {
            return false;
        }

        var charOffsets = CreateCodepointCharOffsets(codepoints);
        var advances = MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, viewport, assetLoader);
        if (advances.Length != codepoints.Count)
        {
            return false;
        }

        var bidiSources = new List<TextDomClusterSource>(codepoints.Count);
        for (var runIndex = 0; runIndex < visualRuns.Count; runIndex++)
        {
            var run = visualRuns[runIndex];
            if (!TryGetCodepointRange(charOffsets, run.StartCharIndex, run.StartCharIndex + run.Length, out var startCodepointIndex, out var endCodepointIndex))
            {
                return false;
            }

            if (run.Direction == SvgTextDirection.RightToLeft && !preserveLogicalRtlPlacement)
            {
                for (var i = endCodepointIndex - 1; i >= startCodepointIndex; i--)
                {
                    AppendBidiClusterSource(codepoints, charOffsets, advances, i, bidiSources, ref totalAdvance);
                }

                continue;
            }

            for (var i = startCodepointIndex; i < endCodepointIndex; i++)
            {
                AppendBidiClusterSource(codepoints, charOffsets, advances, i, bidiSources, ref totalAdvance);
            }
        }

        sources = bidiSources.ToArray();
        return sources.Length > 0;
    }

    private static void AppendBidiClusterSource(
        IReadOnlyList<string> codepoints,
        IReadOnlyList<int> charOffsets,
        IReadOnlyList<float> advances,
        int codepointIndex,
        List<TextDomClusterSource> sources,
        ref float totalAdvance)
    {
        var advance = codepointIndex >= 0 && codepointIndex < advances.Count
            ? Math.Max(0f, advances[codepointIndex])
            : 0f;
        sources.Add(new TextDomClusterSource(
            codepointIndex,
            codepoints[codepointIndex],
            totalAdvance,
            advance)
        {
            FirstCharIndex = charOffsets[codepointIndex]
        });
        totalAdvance += advance;
    }

    private static SvgTextDirection ResolvePlainTextDirection(string text, SvgTextDirection fallback)
    {
        var codepoints = SplitCodepoints(text);
        for (var i = 0; i < codepoints.Count; i++)
        {
            var direction = SvgTextBidiResolver.GetStrongDirection(codepoints[i]);
            if (direction > 0)
            {
                return SvgTextDirection.LeftToRight;
            }

            if (direction < 0)
            {
                return SvgTextDirection.RightToLeft;
            }
        }

        return fallback;
    }

    private static bool HasPlainTextParagraphSeparator(string text)
    {
        return text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0;
    }

    private static bool TryGetCodepointRange(
        IReadOnlyList<int> charOffsets,
        int startCharIndex,
        int endCharIndex,
        out int startCodepointIndex,
        out int endCodepointIndex)
    {
        startCodepointIndex = GetCodepointBoundaryIndexFromCharOffset(charOffsets, startCharIndex);
        endCodepointIndex = GetCodepointBoundaryIndexFromCharOffset(charOffsets, endCharIndex);
        return startCodepointIndex >= 0 &&
               endCodepointIndex >= startCodepointIndex &&
               endCodepointIndex < charOffsets.Count;
    }

    private static int GetCodepointBoundaryIndexFromCharOffset(IReadOnlyList<int> charOffsets, int charOffset)
    {
        for (var i = 0; i < charOffsets.Count; i++)
        {
            if (charOffsets[i] == charOffset)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryCreateSvgFontClusterSources(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out TextDomClusterSource[] sources,
        out float totalAdvance)
    {
        sources = Array.Empty<TextDomClusterSource>();
        totalAdvance = 0f;
        if (!SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, includeGlyphTexts: true, out var svgFontLayout) ||
            svgFontLayout is null)
        {
            return false;
        }

        var placements = svgFontLayout.GlyphPlacements;
        var placementTexts = svgFontLayout.GlyphTexts;
        if (placements.Count == 0 || placementTexts is null || placementTexts.Count != placements.Count)
        {
            return false;
        }

        var clusterSources = new List<TextDomClusterSource>(placements.Count);
        var codepointIndex = 0;
        for (var i = 0; i < placements.Count; i++)
        {
            if (placementTexts[i].Length == 0)
            {
                continue;
            }

            var nextRelativeOffset = GetNextSvgFontClusterRelativeOffset(placements, placementTexts, i + 1, svgFontLayout.Advance);
            clusterSources.Add(new TextDomClusterSource(
                codepointIndex,
                placementTexts[i],
                placements[i].RelativeX,
                Math.Max(0f, nextRelativeOffset - placements[i].RelativeX)));
            codepointIndex += CountCodepoints(placementTexts[i]);
        }

        sources = clusterSources.ToArray();
        totalAdvance = svgFontLayout.Advance;
        return true;
    }

    private static float GetNextSvgFontClusterRelativeOffset(
        IReadOnlyList<SvgFontTextRenderer.SvgGlyphPlacementResult> placements,
        IReadOnlyList<string> placementTexts,
        int startIndex,
        float fallbackOffset)
    {
        for (var i = startIndex; i < placements.Count && i < placementTexts.Count; i++)
        {
            if (placementTexts[i].Length > 0)
            {
                return placements[i].RelativeX;
            }
        }

        return fallbackOffset;
    }

    private static bool TryCreateShapedClusterSources(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out TextDomClusterSource[] sources,
        out float totalAdvance)
    {
        sources = Array.Empty<TextDomClusterSource>();
        totalAdvance = 0f;
        if (!TryCreateSingleRunShapingPaint(text, paint, assetLoader, out var shapingPaint) ||
            !TryShapeCodepointRun(text, shapingPaint, assetLoader, out var shapedRun))
        {
            return false;
        }

        var codepoints = SplitCodepoints(text);
        if (codepoints.Count == 0 ||
            shapedRun.Glyphs.Length == 0 ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length ||
            shapedRun.Clusters.Length != shapedRun.Glyphs.Length)
        {
            return false;
        }

        var codepointCharOffsets = CreateCodepointCharOffsets(codepoints);
        var clusterSources = new List<TextDomClusterSource>();
        var glyphIndex = 0;
        while (glyphIndex < shapedRun.Glyphs.Length)
        {
            var clusterCharIndex = shapedRun.Clusters[glyphIndex];
            var codepointIndex = GetCodepointIndexFromCharOffset(codepointCharOffsets, clusterCharIndex);
            if (codepointIndex < 0)
            {
                return false;
            }

            var clusterPointX = shapedRun.Points[glyphIndex].X;
            glyphIndex++;
            while (glyphIndex < shapedRun.Glyphs.Length && shapedRun.Clusters[glyphIndex] == clusterCharIndex)
            {
                glyphIndex++;
            }

            var nextClusterCharIndex = glyphIndex < shapedRun.Clusters.Length
                ? shapedRun.Clusters[glyphIndex]
                : text.Length;
            if (nextClusterCharIndex <= clusterCharIndex || nextClusterCharIndex > text.Length)
            {
                return false;
            }

            var nextPointX = glyphIndex < shapedRun.Points.Length
                ? shapedRun.Points[glyphIndex].X
                : shapedRun.Advance;
            clusterSources.Add(new TextDomClusterSource(
                codepointIndex,
                text.Substring(clusterCharIndex, nextClusterCharIndex - clusterCharIndex),
                clusterPointX,
                Math.Max(0f, nextPointX - clusterPointX)));
        }

        sources = MergeTextDomClusterSourcesByGraphemeClusters(text, clusterSources);
        totalAdvance = shapedRun.Advance;
        return sources.Length > 0;
    }

    private static TextDomClusterSource[] MergeTextDomClusterSourcesByGraphemeClusters(
        string text,
        IReadOnlyList<TextDomClusterSource> sources)
    {
        if (sources.Count <= 1)
        {
            return CopyTextDomClusterSources(sources);
        }

        var clusterStarts = SvgTextBoundaryResolver.Default.GetGraphemeClusterStartCharIndexes(text);
        if (clusterStarts.Count <= 1 || clusterStarts.Count == sources.Count)
        {
            return CopyTextDomClusterSources(sources);
        }

        var codepoints = SplitCodepoints(text);
        var codepointCharOffsets = CreateCodepointCharOffsets(codepoints);
        var mergedSources = new List<TextDomClusterSource>(clusterStarts.Count);
        var sourceIndex = 0;
        for (var clusterIndex = 0; clusterIndex < clusterStarts.Count; clusterIndex++)
        {
            var clusterStart = clusterStarts[clusterIndex];
            var clusterEnd = clusterIndex + 1 < clusterStarts.Count
                ? clusterStarts[clusterIndex + 1]
                : text.Length;
            var firstCodepointIndex = GetCodepointIndexFromCharOffset(codepointCharOffsets, clusterStart);
            var endCodepointIndex = GetTextPathCodepointBoundaryIndex(codepointCharOffsets, clusterEnd);
            if (firstCodepointIndex < 0 ||
                endCodepointIndex <= firstCodepointIndex ||
                sourceIndex >= sources.Count)
            {
                return CopyTextDomClusterSources(sources);
            }

            var firstSource = sources[sourceIndex];
            if (firstSource.FirstCodepointIndex != firstCodepointIndex)
            {
                return CopyTextDomClusterSources(sources);
            }

            var advance = 0f;
            var nextCodepointIndex = firstCodepointIndex;
            while (sourceIndex < sources.Count &&
                   sources[sourceIndex].FirstCodepointIndex < endCodepointIndex)
            {
                var source = sources[sourceIndex];
                if (source.FirstCodepointIndex != nextCodepointIndex)
                {
                    return CopyTextDomClusterSources(sources);
                }

                advance += source.Advance;
                nextCodepointIndex += CountCodepoints(source.Text);
                sourceIndex++;
            }

            if (nextCodepointIndex != endCodepointIndex)
            {
                return CopyTextDomClusterSources(sources);
            }

            mergedSources.Add(new TextDomClusterSource(
                firstCodepointIndex,
                text.Substring(clusterStart, clusterEnd - clusterStart),
                firstSource.RelativeOffset,
                advance));
        }

        return sourceIndex == sources.Count
            ? mergedSources.ToArray()
            : CopyTextDomClusterSources(sources);
    }

    private static TextDomClusterSource[] CopyTextDomClusterSources(IReadOnlyList<TextDomClusterSource> sources)
    {
        if (sources is TextDomClusterSource[] array)
        {
            return array;
        }

        var copy = new TextDomClusterSource[sources.Count];
        for (var i = 0; i < sources.Count; i++)
        {
            copy[i] = sources[i];
        }

        return copy;
    }

    private static TextDomClusterSource[] CreateFallbackClusterSources(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out float totalAdvance)
    {
        var codepoints = SplitCodepoints(text);
        var advances = MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, viewport, assetLoader);
        var clusterStarts = SvgTextBoundaryResolver.Default.GetGraphemeClusterStartCharIndexes(text);
        var codepointCharOffsets = CreateCodepointCharOffsets(codepoints);
        if (clusterStarts.Count >= codepoints.Count)
        {
            return CreateCodepointFallbackClusterSources(codepoints, advances, out totalAdvance);
        }

        if (clusterStarts.Count > 0)
        {
            var clusterSources = new List<TextDomClusterSource>(clusterStarts.Count);
            totalAdvance = 0f;
            for (var clusterIndex = 0; clusterIndex < clusterStarts.Count; clusterIndex++)
            {
                var clusterStart = clusterStarts[clusterIndex];
                var clusterEnd = clusterIndex + 1 < clusterStarts.Count
                    ? clusterStarts[clusterIndex + 1]
                    : text.Length;
                var firstCodepointIndex = GetCodepointIndexFromCharOffset(codepointCharOffsets, clusterStart);
                var endCodepointIndex = GetTextPathCodepointBoundaryIndex(codepointCharOffsets, clusterEnd);
                if (firstCodepointIndex < 0 ||
                    endCodepointIndex <= firstCodepointIndex ||
                    clusterEnd <= clusterStart ||
                    clusterEnd > text.Length)
                {
                    return CreateCodepointFallbackClusterSources(codepoints, advances, out totalAdvance);
                }

                var clusterAdvance = 0f;
                for (var i = firstCodepointIndex; i < endCodepointIndex && i < advances.Length; i++)
                {
                    clusterAdvance += advances[i];
                }

                clusterSources.Add(new TextDomClusterSource(
                    firstCodepointIndex,
                    text.Substring(clusterStart, clusterEnd - clusterStart),
                    totalAdvance,
                    clusterAdvance));
                totalAdvance += clusterAdvance;
            }

            return clusterSources.ToArray();
        }

        return CreateCodepointFallbackClusterSources(codepoints, advances, out totalAdvance);
    }

    private static TextDomClusterSource[] CreateCodepointFallbackClusterSources(
        IReadOnlyList<string> codepoints,
        IReadOnlyList<float> advances,
        out float totalAdvance)
    {
        var sources = new TextDomClusterSource[codepoints.Count];
        totalAdvance = 0f;
        for (var i = 0; i < codepoints.Count; i++)
        {
            sources[i] = new TextDomClusterSource(i, codepoints[i], totalAdvance, advances[i]);
            totalAdvance += advances[i];
        }

        return sources;
    }

    private static int[] CreateCodepointCharOffsets(IReadOnlyList<string> codepoints)
    {
        var offsets = new int[codepoints.Count + 1];
        var charIndex = 0;
        for (var i = 0; i < codepoints.Count; i++)
        {
            offsets[i] = charIndex;
            charIndex += codepoints[i].Length;
        }

        offsets[codepoints.Count] = charIndex;
        return offsets;
    }

    private static int GetCodepointIndexFromCharOffset(IReadOnlyList<int> codepointCharOffsets, int charOffset)
    {
        for (var i = 0; i < codepointCharOffsets.Count - 1; i++)
        {
            if (codepointCharOffsets[i] == charOffset)
            {
                return i;
            }
        }

        return -1;
    }

    private static SKRect CreateFallbackTextDomClusterBounds(
        SvgTextBase svgTextBase,
        SKPoint point,
        float advance,
        float scaleX,
        SKPoint scalePivot,
        float rotationDegrees,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        string text,
        out SKRect hitExtent)
    {
        var metrics = assetLoader.GetFontMetrics(paint);
        var cellBounds = new SKRect(
            point.X,
            point.Y + metrics.Ascent,
            point.X + advance,
            point.Y + metrics.Descent);
        var bounds = cellBounds;
        if (!IsWhitespaceOnlyText(text) &&
            TryGetRenderedTextLocalBounds(text, paint, assetLoader, out var glyphBounds))
        {
            hitExtent = new SKRect(
                point.X + glyphBounds.Left,
                point.Y + glyphBounds.Top,
                point.X + glyphBounds.Right,
                point.Y + glyphBounds.Bottom);
        }
        else
        {
            hitExtent = cellBounds;
        }

        bounds = ExpandTextBoundsWithAdvanceBox(svgTextBase, bounds, point.X, point.Y, advance, paint, assetLoader);
        bounds = ScaleBoundsX(bounds, scalePivot, scaleX);
        hitExtent = ScaleBoundsX(hitExtent, scalePivot, scaleX);
        hitExtent = RotateBounds(hitExtent, point, rotationDegrees);
        return RotateBounds(bounds, point, rotationDegrees);
    }

    private static bool TryMeasureTextDomClusterBounds(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement placement,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKRect bounds,
        out SKRect hitExtent,
        out float advance)
    {
        hitExtent = SKRect.Empty;
        var localPaint = paint.Clone();
        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, localPaint, assetLoader, out var svgFontLayout) &&
            svgFontLayout is not null)
        {
            bounds = svgFontLayout.GetBounds(placement.Point.X, placement.Point.Y);
            hitExtent = IsWhitespaceOnlyText(text) || bounds.IsEmpty
                ? GetTextAdvanceBox(svgTextBase, placement.Point.X, placement.Point.Y, svgFontLayout.Advance, localPaint, assetLoader)
                : bounds;
            bounds = ExpandTextBoundsWithHorizontalAdvanceBox(bounds, placement.Point.X, svgFontLayout.Advance);
            bounds = ScaleBoundsX(bounds, GetScalePivot(placement), placement.ScaleX);
            bounds = RotateBounds(bounds, placement.Point, placement.RotationDegrees);
            hitExtent = ScaleBoundsX(hitExtent, GetScalePivot(placement), placement.ScaleX);
            hitExtent = RotateBounds(hitExtent, placement.Point, placement.RotationDegrees);
            advance = svgFontLayout.Advance * placement.ScaleX;
            return true;
        }

        var resolved = ResolveFallbackCodepoint(svgTextBase, text, localPaint, assetLoader);
        if (TryGetRenderedTextLocalBounds(resolved.Text, resolved.Paint, assetLoader, out var glyphBounds))
        {
            bounds = new SKRect(
                placement.Point.X + glyphBounds.Left,
                placement.Point.Y + glyphBounds.Top,
                placement.Point.X + glyphBounds.Right,
                placement.Point.Y + glyphBounds.Bottom);
            hitExtent = IsWhitespaceOnlyText(text)
                ? GetTextAdvanceBox(svgTextBase, placement.Point.X, placement.Point.Y, resolved.Advance, resolved.Paint, assetLoader)
                : bounds;
        }
        else
        {
            var metrics = assetLoader.GetFontMetrics(resolved.Paint);
            bounds = new SKRect(
                placement.Point.X,
                placement.Point.Y + metrics.Ascent,
                placement.Point.X + resolved.Advance,
                placement.Point.Y + metrics.Descent);
            hitExtent = bounds;
        }

        bounds = ExpandTextBoundsWithAdvanceBox(svgTextBase, bounds, placement.Point.X, placement.Point.Y, resolved.Advance, resolved.Paint, assetLoader);
        bounds = ScaleBoundsX(bounds, GetScalePivot(placement), placement.ScaleX);
        bounds = RotateBounds(bounds, placement.Point, placement.RotationDegrees);
        hitExtent = ScaleBoundsX(hitExtent, GetScalePivot(placement), placement.ScaleX);
        hitExtent = RotateBounds(hitExtent, placement.Point, placement.RotationDegrees);
        advance = resolved.Advance * placement.ScaleX;
        return true;
    }

    private static SKRect ExpandTextBoundsWithHorizontalAdvanceBox(SKRect bounds, float originX, float advance)
    {
        if (!IsValidPositiveAdvance(Math.Abs(advance)))
        {
            return bounds;
        }

        var advanceEnd = originX + advance;
        var left = Math.Min(originX, advanceEnd);
        var right = Math.Max(originX, advanceEnd);
        return new SKRect(
            Math.Min(bounds.Left, left),
            bounds.Top,
            Math.Max(bounds.Right, right),
            bounds.Bottom);
    }

}
