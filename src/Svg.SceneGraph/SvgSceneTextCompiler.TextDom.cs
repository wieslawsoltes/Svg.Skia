using System;
using System.Collections.Generic;
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
        private readonly int[] _clusterIndicesByChar;

        internal static SvgTextContentMetrics Empty { get; } = new(Array.Empty<TextDomClusterMetric>(), Array.Empty<int>(), 0f);

        public SvgTextContentMetrics(
            TextDomClusterMetric[] clusters,
            int[] clusterIndicesByChar,
            float computedTextLength)
        {
            _clusters = clusters;
            _clusterIndicesByChar = clusterIndicesByChar;
            ComputedTextLength = computedTextLength;
        }

        public int NumberOfChars => _clusterIndicesByChar.Length;

        public float ComputedTextLength { get; }

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

            var endCharIndex = Math.Min(NumberOfChars - 1, charnum + nchars - 1);
            var startCluster = GetCluster(charnum);
            var endCluster = GetCluster(endCharIndex);
            return Math.Max(0f, endCluster.EndOffset - startCluster.StartOffset);
        }

        public SKPoint GetStartPositionOfChar(int charnum)
        {
            return GetCluster(charnum).StartPoint;
        }

        public SKPoint GetEndPositionOfChar(int charnum)
        {
            return GetCluster(charnum).EndPoint;
        }

        public SKRect GetExtentOfChar(int charnum)
        {
            return GetCluster(charnum).Extent;
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

        private TextDomClusterMetric GetCluster(int charnum)
        {
            ValidateCharacterIndex(charnum);
            return _clusters[_clusterIndicesByChar[charnum]];
        }

        private void ValidateCharacterIndex(int charnum)
        {
            if (charnum < 0 || charnum >= NumberOfChars)
            {
                throw new ArgumentOutOfRangeException(nameof(charnum));
            }
        }

        private static bool TryGetCharacterIndexAtPosition(TextDomClusterMetric cluster, SKPoint point, out int charIndex)
        {
            charIndex = -1;
            if (cluster.CharLength <= 0 ||
                cluster.Extent.IsEmpty ||
                point.X < cluster.Extent.Left ||
                point.X > cluster.Extent.Right ||
                point.Y < cluster.Extent.Top ||
                point.Y > cluster.Extent.Bottom)
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

            var segmentLength = distance / cluster.CharLength;
            if (segmentLength <= 0f)
            {
                charIndex = cluster.StartCharIndex;
                return true;
            }

            var relativeCharIndex = Math.Min(cluster.CharLength - 1, (int)Math.Floor(inlineOffset / segmentLength));
            charIndex = cluster.StartCharIndex + relativeCharIndex;
            return true;
        }
    }

    private sealed class SvgTextContentMetricsBuilder
    {
        private readonly List<TextDomClusterMetric> _clusters = new();
        private readonly List<int> _clusterIndicesByChar = new();
        private float _computedTextLength;

        public void AppendRun(IReadOnlyList<TextDomRunClusterMetric> runClusters, float runLength)
        {
            var runStartOffset = _computedTextLength;
            for (var clusterIndex = 0; clusterIndex < runClusters.Count; clusterIndex++)
            {
                var runCluster = runClusters[clusterIndex];
                if (string.IsNullOrEmpty(runCluster.Text))
                {
                    continue;
                }

                var startCharIndex = _clusterIndicesByChar.Count;
                var charLength = runCluster.Text.Length;
                var globalCluster = new TextDomClusterMetric(
                    startCharIndex,
                    charLength,
                    runStartOffset + runCluster.StartOffset,
                    runStartOffset + runCluster.EndOffset,
                    runCluster.StartPoint,
                    runCluster.EndPoint,
                    runCluster.Extent,
                    runCluster.RotationDegrees);
                _clusters.Add(globalCluster);
                for (var charIndex = 0; charIndex < charLength; charIndex++)
                {
                    _clusterIndicesByChar.Add(_clusters.Count - 1);
                }
            }

            _computedTextLength = runStartOffset + Math.Max(0f, runLength);
        }

        public SvgTextContentMetrics Build()
        {
            return new SvgTextContentMetrics(
                _clusters.ToArray(),
                _clusterIndicesByChar.ToArray(),
                _computedTextLength);
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
        float RotationDegrees);

    private readonly record struct TextDomRunClusterMetric(
        string Text,
        float StartOffset,
        float EndOffset,
        SKPoint StartPoint,
        SKPoint EndPoint,
        SKRect Extent,
        float RotationDegrees);

    private readonly record struct TextDomClusterSource(
        int FirstCodepointIndex,
        string Text,
        float RelativeOffset,
        float Advance);

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
        if (TryCreateSequentialTextContentMetrics(svgTextBase, viewport, assetLoader, out metrics))
        {
            return true;
        }

        var builder = new SvgTextContentMetricsBuilder();
        var currentX = 0f;
        var currentY = 0f;
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
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport);
        currentX += baselineShift.X;
        currentY += baselineShift.Y;
        ApplyInitialChildContainerOffsets(svgTextBase, viewport, assetLoader, ref currentX, ref currentY);

        var totalAdvance = MeasureSequentialTextRuns(plainRuns, viewport, assetLoader);
        var textAlign = GetTextAnchorAlign(svgTextBase, viewport);
        var inlineOrigin = GetAlignedStartCoordinate(currentX, totalAdvance, textAlign);
        var drawX = inlineOrigin;
        var drawY = currentY;
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
                        styleSource.SpaceHandling != XmlSpaceHandling.Preserve &&
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
                    previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
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
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport);
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
                        svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text) &&
                        !string.IsNullOrWhiteSpace(rawContent) &&
                        svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
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
                        previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(codepointCount);
                        break;
                    }

                    var x = useInitialPosition && xs.Count >= 1 ? xs[0] : currentX;
                    var y = useInitialPosition && ys.Count >= 1 ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;

                    if (!TryCreateTextRunMetrics(svgTextBase, text!, currentX, currentY, viewport, assetLoader, rotations, false, out var runClusters, out var runLength))
                    {
                        return false;
                    }

                    builder.AppendRun(runClusters, runLength);
                    ApplyInlineAdvance(svgTextBase, ref currentX, ref currentY, runLength);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
                    absolutePositionState?.Consume(codepointCount);
                    break;

                case SvgTextPath:
                case SvgTextRef:
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
        var textAlign = forceLeftAlign ? SKTextAlign.Left : paint.TextAlign;

        if (TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, viewport, textAlign, assetLoader, rotations, out _, out _))
        {
            return false;
        }

        if (TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, viewport, textAlign, assetLoader, rotations, out var placements, out _))
        {
            return TryCreatePlacedTextRunMetrics(svgTextBase, text, viewport, assetLoader, placements, out clusters, out runLength);
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

    private static bool TryCreatePlacedTextRunMetrics(
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
        if (!TryCreateClusterSources(svgTextBase, text, viewport, paint, assetLoader, out var sources, out _))
        {
            return false;
        }

        var origin = placements[0].Point.X;
        var runClusters = new TextDomRunClusterMetric[sources.Length];
        var maxEndOffset = 0f;
        for (var i = 0; i < sources.Length; i++)
        {
            var source = sources[i];
            if (source.FirstCodepointIndex < 0 || source.FirstCodepointIndex >= placements.Length)
            {
                return false;
            }

            var placement = placements[source.FirstCodepointIndex];
            var startOffset = placement.Point.X - origin;
            var endOffset = startOffset + (source.Advance * placement.ScaleX);
            if (!TryMeasureTextDomClusterBounds(svgTextBase, source.Text, placement, paint, assetLoader, out var extent, out _))
            {
                extent = SKRect.Empty;
            }

            var startPoint = placement.Point;
            var endPoint = new SKPoint(placement.Point.X + (source.Advance * placement.ScaleX), placement.Point.Y);
            runClusters[i] = new TextDomRunClusterMetric(
                source.Text,
                startOffset,
                endOffset,
                startPoint,
                endPoint,
                extent,
                placement.RotationDegrees);
            maxEndOffset = Math.Max(maxEndOffset, endOffset);
        }

        clusters = runClusters;
        runLength = maxEndOffset;
        return true;
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
            if (!TryMeasureTextDomClusterBounds(svgTextBase, source.Text, placement, paint, assetLoader, out var extent, out _))
            {
                extent = SKRect.Empty;
            }

            runClusters[i] = new TextDomRunClusterMetric(
                source.Text,
                source.RelativeOffset,
                source.RelativeOffset + source.Advance,
                placement.Point,
                new SKPoint(placement.Point.X + source.Advance, placement.Point.Y),
                extent,
                0f);
        }

        clusters = runClusters;
        runLength = totalAdvance;
        return true;
    }

    private static bool TryCreateClusterSources(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out TextDomClusterSource[] sources,
        out float totalAdvance)
    {
        if (TryCreateSvgFontClusterSources(svgTextBase, text, paint, assetLoader, out sources, out totalAdvance))
        {
            return true;
        }

        if (TryCreateShapedClusterSources(svgTextBase, text, viewport, paint, assetLoader, out sources, out totalAdvance))
        {
            return true;
        }

        sources = CreateFallbackClusterSources(svgTextBase, text, viewport, assetLoader, out totalAdvance);
        return sources.Length > 0;
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

        var clusterSources = new TextDomClusterSource[placements.Count];
        var codepointIndex = 0;
        for (var i = 0; i < placements.Count; i++)
        {
            var nextRelativeOffset = i + 1 < placements.Count
                ? placements[i + 1].RelativeX
                : svgFontLayout.Advance;
            clusterSources[i] = new TextDomClusterSource(
                codepointIndex,
                placementTexts[i],
                placements[i].RelativeX,
                Math.Max(0f, nextRelativeOffset - placements[i].RelativeX));
            codepointIndex += CountCodepoints(placementTexts[i]);
        }

        sources = clusterSources;
        totalAdvance = svgFontLayout.Advance;
        return true;
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

        sources = clusterSources.ToArray();
        totalAdvance = shapedRun.Advance;
        return sources.Length > 0;
    }

    private static TextDomClusterSource[] CreateFallbackClusterSources(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out float totalAdvance)
    {
        var codepoints = SplitCodepoints(text);
        var advances = MeasureNaturalCodepointAdvances(svgTextBase, codepoints, viewport, assetLoader);
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

    private static bool TryMeasureTextDomClusterBounds(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement placement,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKRect bounds,
        out float advance)
    {
        var localPaint = paint.Clone();
        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, localPaint, assetLoader, out var svgFontLayout) &&
            svgFontLayout is not null)
        {
            bounds = svgFontLayout.GetBounds(placement.Point.X, placement.Point.Y);
            bounds = ExpandTextBoundsWithAdvanceBox(svgTextBase, bounds, placement.Point.X, placement.Point.Y, svgFontLayout.Advance, localPaint, assetLoader);
            bounds = ScaleBoundsX(bounds, GetScalePivot(placement), placement.ScaleX);
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
        }
        else
        {
            var metrics = assetLoader.GetFontMetrics(resolved.Paint);
            bounds = new SKRect(
                placement.Point.X,
                placement.Point.Y + metrics.Ascent,
                placement.Point.X + resolved.Advance,
                placement.Point.Y + metrics.Descent);
        }

        bounds = ExpandTextBoundsWithAdvanceBox(svgTextBase, bounds, placement.Point.X, placement.Point.Y, resolved.Advance, resolved.Paint, assetLoader);
        bounds = ScaleBoundsX(bounds, GetScalePivot(placement), placement.ScaleX);
        advance = resolved.Advance * placement.ScaleX;
        return true;
    }
}
