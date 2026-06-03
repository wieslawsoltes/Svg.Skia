#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static partial class SvgSceneTextCompiler
{
    private readonly record struct SharedInlineSizeRunPlacement(
        int SourceCodepointIndex,
        PositionedCodepointPlacement CompilerPlacement,
        SvgTextCodepointPlacement LayoutPlacement,
        float NaturalAdvance,
        float AppliedAdvance);

    private readonly record struct SharedInlineSizeShapedCluster(
        int StartSourceIndex,
        int CodepointCount,
        float Advance)
    {
        public int EndSourceIndex => StartSourceIndex + CodepointCount;

        public bool IsValid => CodepointCount > 0 && Advance >= 0f;
    }

    private sealed class SharedInlineSizeLineBuild
    {
        public SharedInlineSizeLineBuild(
            SvgTextLayoutLine line,
            PositionedCodepointRun[] compilerRuns,
            float advance,
            SKRect bounds)
        {
            Line = line;
            CompilerRuns = compilerRuns;
            Advance = advance;
            Bounds = bounds;
        }

        public SvgTextLayoutLine Line { get; }

        public PositionedCodepointRun[] CompilerRuns { get; }

        public float Advance { get; }

        public SKRect Bounds { get; }
    }

    private static bool TryDrawSharedInlineSizeTextLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SKRect geometryBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool trimLeadingWhitespaceAtStart,
        SvgSceneContextPaint? contextPaint)
    {
        if (!TryCreateSharedInlineSizeTextLayoutResult(
                svgTextBase,
                currentX,
                currentY,
                viewport,
                geometryBounds,
                assetLoader,
                trimLeadingWhitespaceAtStart,
                out var result,
                out var compilerLines,
                out var finalX,
                out var finalY))
        {
            return false;
        }

        for (var lineIndex = 0; lineIndex < compilerLines.Count; lineIndex++)
        {
            var line = compilerLines[lineIndex];
            for (var runIndex = 0; runIndex < line.CompilerRuns.Length; runIndex++)
            {
                var run = line.CompilerRuns[runIndex];
                using var commandSource = PushTextCommandSource(canvas, run.StyleSource, getElementAddressKey);
                _ = DrawTextPaintOrder(run.StyleSource, includeFill: true, includeStroke: true, includeDecorations: true, phase =>
                {
                    switch (phase)
                    {
                        case TextPaintPhase.Fill:
                            if (SvgScenePaintingService.IsValidFill(run.StyleSource))
                            {
                                var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                                if (fillPaint is not null)
                                {
                                    _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader);
                                }
                            }

                            break;

                        case TextPaintPhase.Stroke:
                            if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
                            {
                                var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                                if (strokePaint is not null)
                                {
                                    _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader);
                                }
                            }

                            break;

                        case TextPaintPhase.Decorations:
                            DrawTextDecorations(
                                ResolveTextDecorationLayers(run.StyleSource),
                                run.StyleSource,
                                run.Text,
                                run.Placements,
                                geometryBounds,
                                ignoreAttributes,
                                canvas,
                                assetLoader,
                                contextPaint);
                            break;
                    }

                    return 0f;
                });
            }
        }

        _ = result;
        currentX = finalX;
        currentY = finalY;
        return true;
    }

    private static bool TryMeasureSharedInlineSizeTextLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds,
        bool trimLeadingWhitespaceAtStart)
    {
        if (!TryCreateSharedInlineSizeTextLayoutResult(
                svgTextBase,
                currentX,
                currentY,
                viewport,
                viewport,
                assetLoader,
                trimLeadingWhitespaceAtStart,
                out _,
                out var compilerLines,
                out var finalX,
                out var finalY))
        {
            return false;
        }

        for (var lineIndex = 0; lineIndex < compilerLines.Count; lineIndex++)
        {
            UnionBounds(ref bounds, compilerLines[lineIndex].Bounds);
        }

        currentX = finalX;
        currentY = finalY;
        return true;
    }

    private static bool TryCreateSharedInlineSizeTextContentMetrics(
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
        if (!TryCreateSharedInlineSizeTextLayoutResult(
                svgTextBase,
                currentX,
                currentY,
                viewport,
                viewport,
                assetLoader,
                trimLeadingWhitespaceAtStart: true,
                out var result,
                out _,
                out _,
                out _))
        {
            return false;
        }
        var clusters = new List<TextDomClusterMetric>();
        foreach (var cluster in result.DomMetrics.Clusters)
        {
            clusters.Add(new TextDomClusterMetric(
                cluster.StartCharIndex,
                cluster.CharLength,
                cluster.StartOffset,
                cluster.EndOffset,
                cluster.StartPoint,
                cluster.EndPoint,
                cluster.Extent,
                cluster.RotationDegrees)
            {
                HitExtent = cluster.HitExtent
            });
        }

        if (clusters.Count == 0)
        {
            return false;
        }

        metrics = new SvgTextContentMetrics(clusters.ToArray(), result.DomMetrics.NumberOfChars, result.DomMetrics.ComputedTextLength);
        return metrics.NumberOfChars > 0;
    }

    private static bool TryCreateSharedInlineSizeTextLayoutResult(
        SvgTextBase svgTextBase,
        float currentX,
        float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool trimLeadingWhitespaceAtStart,
        out SvgTextLayoutResult result,
        out IReadOnlyList<SharedInlineSizeLineBuild> compilerLines,
        out float finalX,
        out float finalY)
    {
        result = new SvgTextLayoutResult(null, null, SvgTextDomMetrics.Empty, SKRect.Empty, 0f);
        compilerLines = Array.Empty<SharedInlineSizeLineBuild>();
        finalX = currentX;
        finalY = currentY;

        if (!CanUseSharedInlineSizeTextLayout(svgTextBase))
        {
            return false;
        }

        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);

        var preservePreLineBreaks = PreservesInlineLineBreaksInTextSubtree(svgTextBase);
        if (!TryCollectFlattenedTextCodepoints(
                svgTextBase,
                trimLeadingWhitespaceAtStart,
                viewport,
                assetLoader,
                applyRootPositions: false,
                preservePreLineBreaks: preservePreLineBreaks,
                out var flattenedCodepoints) ||
            flattenedCodepoints.Count == 0 ||
            !AllowsInlineSizeWrapping(flattenedCodepoints))
        {
            return false;
        }

        var naturalAdvances = new float[flattenedCodepoints.Count];
        var preparedRuns = new PreparedSequentialRun[flattenedCodepoints.Count];
        for (var i = 0; i < flattenedCodepoints.Count; i++)
        {
            naturalAdvances[i] = MeasureNaturalTextAdvance(flattenedCodepoints[i].StyleSource, flattenedCodepoints[i].Codepoint, geometryBounds, assetLoader);
            preparedRuns[i] = new PreparedSequentialRun(flattenedCodepoints[i].StyleSource, flattenedCodepoints[i].Codepoint, naturalAdvances[i]);
        }

        var charStarts = CreateFlattenedUtf16Starts(flattenedCodepoints);
        var renderedCharStarts = CreateSharedRenderedUtf16Starts(flattenedCodepoints);
        var shapedClusters = TryCreateSharedShapedClusters(
            flattenedCodepoints,
            charStarts,
            naturalAdvances,
            geometryBounds,
            assetLoader,
            out var resolvedShapedClusters)
            ? resolvedShapedClusters
            : null;
        var textAlign = GetTextAnchorAlign(svgTextBase, geometryBounds);
        if (!TryCreateInlineSizeTextArea(
                svgTextBase,
                preparedRuns,
                currentX,
                currentY,
                viewport,
                geometryBounds,
                assetLoader,
                textAlign,
                out var textArea,
                out var firstLineBlockCoordinate) ||
            textArea is null ||
            GetInlineSizeTextAreaExtent(textArea) <= 0f)
        {
            return false;
        }
        var segments = CreateSharedInlineSizeWrapSegments(flattenedCodepoints, naturalAdvances, shapedClusters);
        if (segments.Count == 0)
        {
            return false;
        }

        var flow = GetInlineSizeFlow(svgTextBase);
        var isVertical = IsVerticalInlineSizeFlow(flow);
        var blockProgression = GetInlineSizeBlockProgression(flow);
        var lineAdvance = GetInlineSizeLineAdvance(preparedRuns, geometryBounds, assetLoader);
        var maxLineSearchCount = textArea.GetMaxWrappedLineSearchCount(firstLineBlockCoordinate, lineAdvance, segments.Count, blockProgression);
        if (maxLineSearchCount <= 0)
        {
            return false;
        }

        var logicalLines = CreateWrappedInlineSizeLogicalLines(
            segments,
            lineIndex => textArea.ResolveWrappedLineArea(lineIndex, firstLineBlockCoordinate, lineAdvance, blockProgression),
            maxLineSearchCount,
            PreservesLineEdgeWhitespace(flattenedCodepoints));
        if (logicalLines.Count == 0)
        {
            return false;
        }

        var hasRootTextLength = TryGetOwnTextLength(svgTextBase, viewport, isVertical, out var rootTextLength) &&
                                rootTextLength > 0f &&
                                GetOwnLengthAdjust(svgTextBase) is SvgTextLengthAdjust.Spacing or SvgTextLengthAdjust.SpacingAndGlyphs;
        var rootLineTextLengths = hasRootTextLength
            ? CreateSharedRootTextLengthLineTargets(logicalLines, flattenedCodepoints, naturalAdvances, shapedClusters, geometryBounds, rootTextLength)
            : Array.Empty<float>();
        var builtLines = new List<SharedInlineSizeLineBuild>(logicalLines.Count);
        var layoutLines = new List<SvgTextLayoutLine>(logicalLines.Count);
        var renderCommands = new List<SvgTextRenderCommand>();
        var domClusters = new List<SvgTextDomClusterMetric>();
        var bounds = SKRect.Empty;
        var computedTextLength = 0f;
        var domCharIndex = 0;
        finalX = currentX;
        finalY = currentY;

        for (var i = 0; i < logicalLines.Count; i++)
        {
            if (!TryBuildSharedInlineSizeLine(
                    svgTextBase,
                    logicalLines[i],
                    flow,
                    isVertical,
                    textArea,
                    geometryBounds,
                    viewport,
                    assetLoader,
                    flattenedCodepoints,
                    naturalAdvances,
                    shapedClusters,
                    renderedCharStarts,
                    hasRootTextLength,
                    hasRootTextLength && i < rootLineTextLengths.Length ? rootLineTextLengths[i] : 0f,
                    out var builtLine))
            {
                return false;
            }

            builtLines.Add(builtLine);
            layoutLines.Add(builtLine.Line);
            computedTextLength += builtLine.Advance;
            finalX = isVertical
                ? builtLine.Line.BaselineOrigin.X
                : GetSharedLineFinalX(builtLine.CompilerRuns, builtLine.Line.BaselineOrigin.X);
            finalY = isVertical
                ? GetSharedLineFinalY(builtLine.CompilerRuns, builtLine.Line.BaselineOrigin.Y)
                : builtLine.Line.BaselineOrigin.Y;
            UnionBounds(ref bounds, builtLine.Bounds);

            foreach (var span in builtLine.Line.PositionedSpans)
            {
                renderCommands.Add(new SvgTextPositionedRenderCommand(SvgTextPaintPhase.Fill, span));
                renderCommands.Add(new SvgTextPositionedRenderCommand(SvgTextPaintPhase.Stroke, span));
                renderCommands.Add(new SvgTextPositionedRenderCommand(SvgTextPaintPhase.Decorations, span));
            }

            AppendSharedDomClusters(builtLine.CompilerRuns, builtLine.Line, computedTextLength - builtLine.Advance, viewport, assetLoader, domClusters, ref domCharIndex);
        }

        if (builtLines.Count == 0 || domClusters.Count == 0)
        {
            return false;
        }

        domClusters.Sort(static (left, right) => left.StartCharIndex != right.StartCharIndex
            ? left.StartCharIndex.CompareTo(right.StartCharIndex)
            : left.CharLength.CompareTo(right.CharLength));
        var domMetrics = new SvgTextDomMetrics(domClusters, domCharIndex, computedTextLength);
        result = new SvgTextLayoutResult(layoutLines, renderCommands, domMetrics, bounds, computedTextLength);
        compilerLines = builtLines;
        return true;
    }

    private static bool CanUseSharedInlineSizeTextLayout(SvgTextBase svgTextBase)
    {
        if (!HasInlineSizeLayout(svgTextBase) ||
            HasRotateValues(svgTextBase) ||
            HasNonBaselineShift(svgTextBase) ||
            ContainsUnsupportedSharedInlineSizeTextLayoutContent(svgTextBase))
        {
            return false;
        }

        return HasOwnTextLengthAdjustment(svgTextBase) ||
               HasPositionedDescendantTextChunk(svgTextBase) ||
               ContainsDescendantTextLengthAdjustment(svgTextBase);
    }

    private static bool ContainsUnsupportedSharedInlineSizeTextLayoutContent(SvgElement element)
    {
        foreach (var node in GetContentNodes(element))
        {
            switch (node)
            {
                case SvgTextPath:
                case SvgTextRef:
                    return true;
                case SvgTextBase textBase:
                    if (HasRotateValues(textBase) ||
                        HasNonBaselineShift(textBase) ||
                        ContainsUnsupportedSharedInlineSizeTextLayoutContent(textBase))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool ContainsDescendantTextLengthAdjustment(SvgElement element)
    {
        foreach (var node in GetContentNodes(element))
        {
            if (node is not SvgTextBase textBase)
            {
                continue;
            }

            if (HasOwnTextLengthAdjustment(textBase) ||
                ContainsDescendantTextLengthAdjustment(textBase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateSharedShapedClusters(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<int> charStarts,
        IReadOnlyList<float> naturalAdvances,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out SharedInlineSizeShapedCluster[] shapedClusters)
    {
        shapedClusters = Array.Empty<SharedInlineSizeShapedCluster>();
        if (codepoints.Count == 0 ||
            charStarts.Count < codepoints.Count + 1 ||
            assetLoader is not ISvgTextGlyphClusterResolver clusterResolver)
        {
            return false;
        }

        var result = new SharedInlineSizeShapedCluster[codepoints.Count];
        var hasUsefulCluster = false;
        var start = 0;
        while (start < codepoints.Count)
        {
            var styleSource = codepoints[start].StyleSource;
            var end = start + 1;
            if (!HasSharedInlinePositionAdjustment(codepoints[start]))
            {
                while (end < codepoints.Count &&
                       ReferenceEquals(codepoints[end].StyleSource, styleSource) &&
                       !HasSharedInlinePositionAdjustment(codepoints[end]))
                {
                    end++;
                }
            }

            TryCreateSharedShapedClustersForRange(
                codepoints,
                charStarts,
                naturalAdvances,
                start,
                end - start,
                styleSource,
                geometryBounds,
                assetLoader,
                clusterResolver,
                result,
                ref hasUsefulCluster);

            start = end;
        }

        if (!hasUsefulCluster)
        {
            shapedClusters = Array.Empty<SharedInlineSizeShapedCluster>();
            return false;
        }

        shapedClusters = result;
        return true;
    }

    private static bool TryCreateSharedShapedClustersForRange(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<int> charStarts,
        IReadOnlyList<float> naturalAdvances,
        int start,
        int count,
        SvgTextBase styleSource,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        ISvgTextGlyphClusterResolver clusterResolver,
        SharedInlineSizeShapedCluster[] result,
        ref bool hasUsefulCluster)
    {
        if (count <= 0)
        {
            return false;
        }

        var text = string.Concat(codepoints.Skip(start).Take(count).Select(static item => item.Codepoint));
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(styleSource, geometryBounds);
        paint.TextAlign = SKTextAlign.Left;
        if (!TryCreateSingleRunShapingPaint(text, paint, assetLoader, out var shapingPaint))
        {
            return false;
        }

        if (!clusterResolver.TryShapeGlyphClusters(text, shapingPaint, IsRightToLeft(styleSource), out _, out var clusters) ||
            clusters.Length == 0)
        {
            return false;
        }

        var localStarts = new int[count + 1];
        for (var i = 0; i < count; i++)
        {
            localStarts[i] = charStarts[start + i] - charStarts[start];
        }

        localStarts[count] = text.Length;
        for (var clusterIndex = 0; clusterIndex < clusters.Length; clusterIndex++)
        {
            var cluster = clusters[clusterIndex];
            if (!TryFindSharedClusterCodepointRange(localStarts, cluster.StartCharIndex, cluster.CharLength, out var localCodepointStart, out var localCodepointCount))
            {
                return false;
            }

            var sourceStart = start + localCodepointStart;
            var shapedCluster = new SharedInlineSizeShapedCluster(sourceStart, localCodepointCount, cluster.Advance);
            for (var i = 0; i < localCodepointCount; i++)
            {
                result[sourceStart + i] = shapedCluster;
            }

            var naturalAdvance = 0f;
            for (var i = 0; i < localCodepointCount; i++)
            {
                naturalAdvance += naturalAdvances[sourceStart + i];
            }

            if (localCodepointCount > 1 || Math.Abs(naturalAdvance - cluster.Advance) > TextLengthTolerance)
            {
                hasUsefulCluster = true;
            }
        }

        return true;
    }

    private static bool TryFindSharedClusterCodepointRange(
        IReadOnlyList<int> localStarts,
        int clusterStart,
        int clusterLength,
        out int codepointStart,
        out int codepointCount)
    {
        codepointStart = -1;
        codepointCount = 0;
        if (clusterLength <= 0)
        {
            return false;
        }

        var clusterEnd = clusterStart + clusterLength;
        for (var i = 0; i + 1 < localStarts.Count; i++)
        {
            if (localStarts[i] == clusterStart)
            {
                codepointStart = i;
            }

            if (codepointStart >= 0 && localStarts[i + 1] == clusterEnd)
            {
                codepointCount = i - codepointStart + 1;
                return codepointCount > 0;
            }
        }

        return false;
    }

    private static bool HasSharedInlinePositionAdjustment(FlattenedTextCodepoint codepoint)
    {
        return codepoint.X.HasValue ||
               codepoint.Y.HasValue ||
               codepoint.Dx != 0f ||
               codepoint.Dy != 0f;
    }

    private static bool TryGetSharedShapedCluster(
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        int sourceIndex,
        out SharedInlineSizeShapedCluster cluster)
    {
        cluster = default;
        if (shapedClusters is null ||
            sourceIndex < 0 ||
            sourceIndex >= shapedClusters.Count ||
            !shapedClusters[sourceIndex].IsValid)
        {
            return false;
        }

        cluster = shapedClusters[sourceIndex];
        return true;
    }

    private static bool IsSharedShapedClusterContinuation(
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        int sourceIndex)
    {
        return TryGetSharedShapedCluster(shapedClusters, sourceIndex, out var cluster) &&
               cluster.StartSourceIndex != sourceIndex;
    }

    private static List<InlineSizeTextSegment> CreateSharedInlineSizeWrapSegments(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters)
    {
        var segments = new List<InlineSizeTextSegment>();
        var segmentRuns = new List<InlineSizeTextRun>();
        float segmentAdvance = 0f;
        bool? segmentIsWhitespace = null;

        void FlushSegment()
        {
            if (segmentRuns.Count == 0 || !segmentIsWhitespace.HasValue)
            {
                return;
            }

            segments.Add(new InlineSizeTextSegment(segmentRuns.ToArray(), segmentAdvance, segmentIsWhitespace.Value, ForcesLineBreak: false));
            segmentRuns.Clear();
            segmentAdvance = 0f;
            segmentIsWhitespace = null;
        }

        void AppendLineBreak()
        {
            FlushSegment();
            segments.Add(new InlineSizeTextSegment(Array.Empty<InlineSizeTextRun>(), 0f, IsWhitespace: true, ForcesLineBreak: true));
        }

        void AppendCodepoint(int codepointIndex, bool isWhitespace, float wrapAdvance)
        {
            if (segmentIsWhitespace.HasValue && segmentIsWhitespace.Value != isWhitespace)
            {
                FlushSegment();
            }

            var codepoint = codepoints[codepointIndex];
            segmentIsWhitespace = isWhitespace;
            segmentRuns.Add(new InlineSizeTextRun(codepoint.StyleSource, codepoint.Codepoint, naturalAdvances[codepointIndex], codepointIndex));
            segmentAdvance += Math.Max(0f, wrapAdvance);
        }

        var codepointTexts = new FlattenedCodepointTextList(codepoints);
        var bidiFormattingDepth = 0;
        string? previousCodepoint = null;
        for (var i = 0; i < codepoints.Count; i++)
        {
            if (IsSharedShapedClusterContinuation(shapedClusters, i))
            {
                continue;
            }

            var codepoint = codepoints[i].Codepoint;
            var styleSource = codepoints[i].StyleSource;
            var nextCodepoint = i + 1 < codepoints.Count ? codepoints[i + 1].Codepoint : null;
            if (codepoint == "\n")
            {
                AppendLineBreak();
                previousCodepoint = null;
                continue;
            }

            if ((codepoints[i].X.HasValue || codepoints[i].Y.HasValue) && segmentRuns.Count > 0)
            {
                AppendLineBreak();
                previousCodepoint = null;
            }

            if (TryGetSharedShapedCluster(shapedClusters, i, out var shapedCluster) &&
                shapedCluster.StartSourceIndex == i &&
                shapedCluster.CodepointCount > 1)
            {
                var clusterIsWhitespace = true;
                for (var clusterOffset = 0; clusterOffset < shapedCluster.CodepointCount; clusterOffset++)
                {
                    if (!IsInlineSizeBreakOpportunityWhitespace(codepoints[i + clusterOffset].Codepoint))
                    {
                        clusterIsWhitespace = false;
                        break;
                    }
                }

                for (var clusterOffset = 0; clusterOffset < shapedCluster.CodepointCount; clusterOffset++)
                {
                    var sourceIndex = i + clusterOffset;
                    AppendCodepoint(
                        sourceIndex,
                        clusterIsWhitespace,
                        clusterOffset == 0 ? shapedCluster.Advance : 0f);
                    bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoints[sourceIndex].Codepoint);
                    previousCodepoint = codepoints[sourceIndex].Codepoint;
                }

                var clusterEndIndex = i + shapedCluster.CodepointCount - 1;
                var codepointAfterCluster = clusterEndIndex + 1 < codepoints.Count ? codepoints[clusterEndIndex + 1].Codepoint : null;
                if (!clusterIsWhitespace &&
                    bidiFormattingDepth == 0 &&
                    !IsInlineSizeNoBreakAdjacentFormatControl(previousCodepoint) &&
                    !IsInlineSizeNoBreakAdjacentFormatControl(codepointAfterCluster) &&
                    IsInlineSizeCharacterBreakOpportunity(codepointTexts, clusterEndIndex, GetInlineSizeLineBreakOptions(styleSource)))
                {
                    FlushSegment();
                }

                i += shapedCluster.CodepointCount - 1;
                continue;
            }

            if (IsInlineSizeInvisibleBreakOpportunity(codepoint, previousCodepoint, nextCodepoint, bidiFormattingDepth > 0))
            {
                FlushSegment();
                previousCodepoint = codepoint;
                continue;
            }

            var codepointIsWhitespace = IsInlineSizeBreakOpportunityWhitespace(
                codepoint,
                previousCodepoint,
                nextCodepoint,
                bidiFormattingDepth > 0);
            var wrapAdvance = codepoints[i].X.HasValue || codepoints[i].Y.HasValue
                ? 0f
                : GetSharedSourceNaturalAdvance(i, naturalAdvances, shapedClusters);
            if (IsSharedShapedClusterContinuation(shapedClusters, i))
            {
                AppendCodepoint(i, codepointIsWhitespace, wrapAdvance);
                bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                previousCodepoint = codepoint;
                continue;
            }

            if (GetInlineSizeWhiteSpaceModel(styleSource).BreaksAfterEveryPreservedSpace && codepointIsWhitespace)
            {
                AppendCodepoint(i, isWhitespace: true, wrapAdvance);
                FlushSegment();
                bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                previousCodepoint = codepoint;
                continue;
            }

            if (!codepointIsWhitespace &&
                bidiFormattingDepth == 0 &&
                !IsInlineSizeNoBreakAdjacentFormatControl(previousCodepoint) &&
                !IsInlineSizeNoBreakAdjacentFormatControl(nextCodepoint) &&
                !IsSharedShapedClusterContinuation(shapedClusters, i + 1) &&
                IsInlineSizeCharacterBreakOpportunity(codepointTexts, i, GetInlineSizeLineBreakOptions(styleSource)))
            {
                AppendCodepoint(i, isWhitespace: false, wrapAdvance);
                FlushSegment();
                bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                previousCodepoint = codepoint;
                continue;
            }

            AppendCodepoint(i, codepointIsWhitespace, wrapAdvance);
            bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
            previousCodepoint = codepoint;
        }

        FlushSegment();
        return segments;
    }

    private static bool TryBuildSharedInlineSizeLine(
        SvgTextBase svgTextBase,
        InlineSizeLogicalLine logicalLine,
        InlineSizeFlow flow,
        bool isVertical,
        InlineSizeTextArea textArea,
        SKRect geometryBounds,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        IReadOnlyList<int> charStarts,
        bool hasRootTextLength,
        float rootLineTextLength,
        out SharedInlineSizeLineBuild builtLine)
    {
        builtLine = null!;
        var logicalSourceIndexes = GetSharedLineSourceIndexes(logicalLine);
        if (logicalSourceIndexes.Length == 0 || logicalLine.Area.InlineSize <= 0f)
        {
            return false;
        }

        var naturalLineAdvance = GetSharedSourceRangeNaturalAdvance(logicalSourceIndexes, 0, logicalSourceIndexes.Length, codepoints, naturalAdvances, shapedClusters, geometryBounds);
        var targetLineAdvance = hasRootTextLength ? rootLineTextLength : naturalLineAdvance;
        var stepAdvances = CreateSharedStepAdvances(logicalSourceIndexes, codepoints, naturalAdvances, shapedClusters, geometryBounds);
        var glyphScales = Enumerable.Repeat(1f, logicalSourceIndexes.Length).ToArray();
        var scaleRunFromStart = new bool[logicalSourceIndexes.Length];
        if (hasRootTextLength)
        {
            ApplySharedTextLengthAdjustment(
                svgTextBase,
                targetLineAdvance,
                logicalSourceIndexes,
                0,
                logicalSourceIndexes.Length,
                codepoints,
                naturalAdvances,
                shapedClusters,
                geometryBounds,
                stepAdvances,
                glyphScales,
                scaleRunFromStart);
        }
        else
        {
            ApplySharedDescendantTextLengthAdjustments(
                logicalSourceIndexes,
                codepoints,
                naturalAdvances,
                shapedClusters,
                geometryBounds,
                viewport,
                stepAdvances,
                glyphScales,
                scaleRunFromStart);
            targetLineAdvance = GetSharedAdjustedLineAdvance(logicalSourceIndexes, naturalAdvances, shapedClusters, stepAdvances, glyphScales);
        }

        var visualSourceIndexes = logicalSourceIndexes;
        var visualStepAdvances = stepAdvances;
        var visualGlyphScales = glyphScales;
        var visualScaleRunFromStart = scaleRunFromStart;
        if (TryCreateSharedVisualLineOrder(
                svgTextBase,
                logicalLine.Runs,
                logicalSourceIndexes,
                stepAdvances,
                glyphScales,
                scaleRunFromStart,
                codepoints,
                naturalAdvances,
                shapedClusters,
                geometryBounds,
                out var reorderedSourceIndexes,
                out var reorderedStepAdvances,
                out var reorderedGlyphScales,
                out var reorderedScaleRunFromStart))
        {
            visualSourceIndexes = reorderedSourceIndexes;
            visualStepAdvances = reorderedStepAdvances;
            visualGlyphScales = reorderedGlyphScales;
            visualScaleRunFromStart = reorderedScaleRunFromStart;
        }

        var renderedAdvance = Math.Max(0f, targetLineAdvance);
        var contentStart = logicalLine.Area.Start;
        var inlineSize = logicalLine.Area.InlineSize;
        var inlineDirection = isVertical ? logicalLine.Area.InlineProgression : 1;
        var blockCoordinate = logicalLine.Area.BlockCoordinate;
        var drawX = isVertical
            ? blockCoordinate
            : IsHorizontalRightToLeftInlineSizeFlow(flow)
                ? contentStart + inlineSize - renderedAdvance
                : contentStart;
        var drawY = isVertical
            ? inlineDirection < 0 ? contentStart + inlineSize : contentStart
            : blockCoordinate;

        var placements = CreateSharedLinePlacements(
            visualSourceIndexes,
            codepoints,
            naturalAdvances,
            visualStepAdvances,
            visualGlyphScales,
            visualScaleRunFromStart,
            drawX,
            drawY);
        if (placements.Length == 0)
        {
            return false;
        }

        var spans = CreateSharedPositionedSpans(
            placements,
            codepoints,
            naturalAdvances,
            charStarts,
            geometryBounds,
            viewport,
            assetLoader,
            new SKPoint(drawX, drawY));
        if (spans.Count == 0)
        {
            return false;
        }

        var compilerRuns = CreateSharedCompilerRuns(placements, codepoints);
        if (compilerRuns.Length == 0)
        {
            return false;
        }

        var lineBounds = SKRect.Empty;
        for (var i = 0; i < compilerRuns.Length; i++)
        {
            var runBounds = MeasureCodepointPlacementBounds(
                compilerRuns[i].StyleSource,
                compilerRuns[i].Text,
                compilerRuns[i].Placements,
                viewport,
                assetLoader,
                out _);
            UnionBounds(ref lineBounds, runBounds);
        }

        var fragments = spans
            .Select(span => new SvgTextLineFragment(
                span.Style,
                span.Text,
                span.SourceRange,
                span.Advance,
                span.Bounds,
                span.Placements,
                isWhitespace: IsWhitespaceOnlyText(span.Text)))
            .ToArray();
        var shapeArea = new SvgTextShapeLineArea(
            logicalLine.LineIndex,
            logicalLine.Area.BlockCoordinate,
            logicalLine.Area.Start,
            logicalLine.Area.InlineSize,
            logicalLine.Area.Fragments?.Select(fragment => new SvgTextShapeInterval(fragment.Start, fragment.End, SvgTextShapeSourceKind.LayoutBox, IsExclusion: false)));
        var layoutLine = new SvgTextLayoutLine(
            logicalLine.LineIndex,
            ToSharedLayoutFlow(flow),
            new SKPoint(drawX, drawY),
            logicalLine.Area.Start,
            logicalLine.Area.InlineSize,
            renderedAdvance,
            lineBounds,
            textArea.IsShapeInside ? shapeArea : null,
            fragments,
            visualFragments: fragments,
            positionedSpans: spans,
            inlineProgression: inlineDirection);

        builtLine = new SharedInlineSizeLineBuild(layoutLine, compilerRuns, renderedAdvance, lineBounds);
        return true;
    }

    private static float[] CreateSharedRootTextLengthLineTargets(
        IReadOnlyList<InlineSizeLogicalLine> logicalLines,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        SKRect geometryBounds,
        float rootTextLength)
    {
        var targets = new float[logicalLines.Count];
        if (logicalLines.Count == 0 || rootTextLength <= 0f)
        {
            return targets;
        }

        var naturalLineAdvances = new float[logicalLines.Count];
        var totalNaturalAdvance = 0f;
        for (var i = 0; i < logicalLines.Count; i++)
        {
            var indexes = GetSharedLineSourceIndexes(logicalLines[i]);
            var naturalAdvance = indexes.Length == 0
                ? 0f
                : GetSharedSourceRangeNaturalAdvance(indexes, 0, indexes.Length, codepoints, naturalAdvances, shapedClusters, geometryBounds);
            naturalLineAdvances[i] = Math.Max(0f, naturalAdvance);
            totalNaturalAdvance += naturalLineAdvances[i];
        }

        if (totalNaturalAdvance <= TextLengthTolerance)
        {
            var equalTarget = rootTextLength / logicalLines.Count;
            for (var i = 0; i < targets.Length; i++)
            {
                targets[i] = equalTarget;
            }

            return targets;
        }

        var assigned = 0f;
        var lastNonEmptyLine = -1;
        for (var i = 0; i < targets.Length; i++)
        {
            if (naturalLineAdvances[i] <= 0f)
            {
                targets[i] = 0f;
                continue;
            }

            lastNonEmptyLine = i;
            targets[i] = rootTextLength * naturalLineAdvances[i] / totalNaturalAdvance;
            assigned += targets[i];
        }

        if (lastNonEmptyLine >= 0)
        {
            targets[lastNonEmptyLine] += rootTextLength - assigned;
        }

        return targets;
    }

    private static int[] GetSharedLineSourceIndexes(InlineSizeLogicalLine logicalLine)
    {
        return GetSharedLineSourceIndexes(logicalLine.Runs);
    }

    private static int[] GetSharedLineSourceIndexes(IReadOnlyList<InlineSizeTextRun> runs)
    {
        var indexes = new List<int>(runs.Count);
        for (var i = 0; i < runs.Count; i++)
        {
            var sourceIndex = runs[i].SourceCodepointIndex;
            if (sourceIndex >= 0)
            {
                indexes.Add(sourceIndex);
            }
        }

        return indexes.ToArray();
    }

    private static bool TryCreateSharedVisualLineOrder(
        SvgTextBase svgTextBase,
        IReadOnlyList<InlineSizeTextRun> logicalRuns,
        IReadOnlyList<int> logicalSourceIndexes,
        IReadOnlyList<float> logicalStepAdvances,
        IReadOnlyList<float> logicalGlyphScales,
        IReadOnlyList<bool> logicalScaleRunFromStart,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        SKRect geometryBounds,
        out int[] visualSourceIndexes,
        out float[] visualStepAdvances,
        out float[] visualGlyphScales,
        out bool[] visualScaleRunFromStart)
    {
        visualSourceIndexes = Array.Empty<int>();
        visualStepAdvances = Array.Empty<float>();
        visualGlyphScales = Array.Empty<float>();
        visualScaleRunFromStart = Array.Empty<bool>();

        if (logicalRuns.Count <= 1 || logicalSourceIndexes.Count != logicalRuns.Count)
        {
            return false;
        }

        var sharedRuns = new SvgSharedTextLayoutRun[logicalRuns.Count];
        for (var i = 0; i < logicalRuns.Count; i++)
        {
            sharedRuns[i] = new SvgSharedTextLayoutRun(
                logicalRuns[i].StyleSource,
                logicalRuns[i].Text,
                logicalRuns[i].Advance,
                logicalRuns[i].SourceCodepointIndex);
        }

        var visualRuns = SvgSharedTextLayoutEngine.CreateLineScopedVisualRuns(svgTextBase, sharedRuns);
        if (visualRuns.Length != logicalRuns.Count)
        {
            return false;
        }

        visualSourceIndexes = GetSharedLineSourceIndexes(Array.ConvertAll(
            visualRuns,
            static run => new InlineSizeTextRun(run.StyleSource, run.Text, run.Advance, run.SourceCodepointIndex)));
        if (visualSourceIndexes.Length != logicalSourceIndexes.Count ||
            HasSameSourceOrder(logicalSourceIndexes, visualSourceIndexes))
        {
            return false;
        }

        var logicalPositionBySourceIndex = new Dictionary<int, int>(logicalSourceIndexes.Count);
        for (var i = 0; i < logicalSourceIndexes.Count; i++)
        {
            if (logicalPositionBySourceIndex.ContainsKey(logicalSourceIndexes[i]))
            {
                return false;
            }

            logicalPositionBySourceIndex.Add(logicalSourceIndexes[i], i);
        }

        visualStepAdvances = new float[visualSourceIndexes.Length];
        visualGlyphScales = new float[visualSourceIndexes.Length];
        visualScaleRunFromStart = new bool[visualSourceIndexes.Length];
        for (var i = 0; i < visualSourceIndexes.Length; i++)
        {
            var sourceIndex = visualSourceIndexes[i];
            if (!logicalPositionBySourceIndex.TryGetValue(sourceIndex, out var logicalPosition))
            {
                return false;
            }

            var glyphScale = logicalGlyphScales[logicalPosition];
            var unitAdvance = GetSharedSourceNaturalAdvance(sourceIndex, naturalAdvances, shapedClusters);
            var adjustedAdvance = unitAdvance * glyphScale;
            var logicalStepAdvance = logicalStepAdvances[logicalPosition];
            var extraSpacing = logicalStepAdvance > 0f
                ? Math.Max(0f, logicalStepAdvance - unitAdvance)
                : GetInterCodepointSpacingAdvance(codepoints[sourceIndex], unitAdvance, geometryBounds);
            visualStepAdvances[i] = Math.Max(0f, adjustedAdvance + extraSpacing);
            visualGlyphScales[i] = glyphScale;
            visualScaleRunFromStart[i] = logicalScaleRunFromStart[logicalPosition];
        }

        return true;
    }

    private static bool HasSameSourceOrder(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static float[] CreateSharedStepAdvances(
        IReadOnlyList<int> sourceIndexes,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        SKRect geometryBounds)
    {
        var stepAdvances = new float[sourceIndexes.Count];
        for (var i = 0; i + 1 < sourceIndexes.Count; i++)
        {
            var sourceIndex = sourceIndexes[i];
            if (TryGetSharedShapedCluster(shapedClusters, sourceIndex, out var cluster) &&
                sourceIndexes[i + 1] < cluster.EndSourceIndex)
            {
                stepAdvances[i] = 0f;
                continue;
            }

            var sourceAdvance = TryGetSharedShapedCluster(shapedClusters, sourceIndex, out cluster)
                ? cluster.Advance
                : GetSharedSourceNaturalAdvance(sourceIndex, naturalAdvances, shapedClusters);
            stepAdvances[i] = sourceAdvance +
                              GetInterCodepointSpacingAdvance(codepoints[sourceIndex], sourceAdvance, geometryBounds);
        }

        return stepAdvances;
    }

    private static void ApplySharedDescendantTextLengthAdjustments(
        IReadOnlyList<int> sourceIndexes,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        SKRect geometryBounds,
        SKRect viewport,
        float[] stepAdvances,
        float[] glyphScales,
        bool[] scaleRunFromStart)
    {
        var start = 0;
        while (start < sourceIndexes.Count)
        {
            var styleSource = codepoints[sourceIndexes[start]].StyleSource;
            var end = start + 1;
            while (end < sourceIndexes.Count && ReferenceEquals(codepoints[sourceIndexes[end]].StyleSource, styleSource))
            {
                end++;
            }

            if (HasOwnTextLengthAdjustment(styleSource) &&
                TryGetOwnTextLength(styleSource, viewport, IsVerticalWritingMode(styleSource), out var textLength) &&
                textLength > 0f)
            {
                ApplySharedTextLengthAdjustment(
                    styleSource,
                    textLength,
                    sourceIndexes,
                    start,
                    end - start,
                    codepoints,
                    naturalAdvances,
                    shapedClusters,
                    geometryBounds,
                    stepAdvances,
                    glyphScales,
                    scaleRunFromStart);
            }

            start = end;
        }
    }

    private static void ApplySharedTextLengthAdjustment(
        SvgTextBase lengthSource,
        float targetAdvance,
        IReadOnlyList<int> sourceIndexes,
        int start,
        int count,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        SKRect geometryBounds,
        float[] stepAdvances,
        float[] glyphScales,
        bool[] scaleRunFromStart)
    {
        if (count <= 0 || targetAdvance <= 0f)
        {
            return;
        }

        var naturalAdvance = GetSharedSourceRangeNaturalAdvance(sourceIndexes, start, count, codepoints, naturalAdvances, shapedClusters, geometryBounds);
        if (naturalAdvance <= 0f || Math.Abs(naturalAdvance - targetAdvance) <= TextLengthTolerance)
        {
            return;
        }

        if (GetOwnLengthAdjust(lengthSource) == SvgTextLengthAdjust.Spacing)
        {
            var gapStepIndexes = GetSharedTextLengthGapStepIndexes(sourceIndexes, start, count, shapedClusters);
            if (gapStepIndexes.Count == 0)
            {
                return;
            }

            var extraGap = (targetAdvance - naturalAdvance) / gapStepIndexes.Count;
            for (var i = 0; i < gapStepIndexes.Count; i++)
            {
                var stepIndex = gapStepIndexes[i];
                stepAdvances[stepIndex] = Math.Max(0f, stepAdvances[stepIndex] + extraGap);
            }

            return;
        }

        var glyphScale = targetAdvance / naturalAdvance;
        if (glyphScale <= 0f)
        {
            return;
        }

        for (var i = start; i < start + count; i++)
        {
            glyphScales[i] = glyphScale;
            scaleRunFromStart[i] = true;
            if (i < start + count - 1)
            {
                stepAdvances[i] = Math.Max(0f, stepAdvances[i] * glyphScale);
            }
        }
    }

    private static float GetSharedSourceRangeNaturalAdvance(
        IReadOnlyList<int> sourceIndexes,
        int start,
        int count,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        SKRect geometryBounds)
    {
        var advance = 0f;
        var end = Math.Min(sourceIndexes.Count, start + count);
        for (var i = start; i < end; i++)
        {
            var sourceIndex = sourceIndexes[i];
            advance += GetSharedSourceNaturalAdvance(sourceIndex, naturalAdvances, shapedClusters);
            if (i < end - 1 && ShouldAddSharedInterUnitSpacing(sourceIndex, sourceIndexes[i + 1], shapedClusters))
            {
                advance += GetInterCodepointSpacingAdvance(
                    codepoints[sourceIndex],
                    GetSharedSourceNaturalAdvance(sourceIndex, naturalAdvances, shapedClusters),
                    geometryBounds);
            }
        }

        return advance;
    }

    private static float GetSharedAdjustedLineAdvance(
        IReadOnlyList<int> sourceIndexes,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters,
        IReadOnlyList<float> stepAdvances,
        IReadOnlyList<float> glyphScales)
    {
        var advance = 0f;
        for (var i = 0; i < sourceIndexes.Count; i++)
        {
            var sourceIndex = sourceIndexes[i];
            if (TryGetSharedShapedCluster(shapedClusters, sourceIndex, out var cluster) &&
                cluster.StartSourceIndex != sourceIndex)
            {
                if (i < sourceIndexes.Count - 1 && sourceIndexes[i + 1] >= cluster.EndSourceIndex)
                {
                    advance += Math.Max(0f, stepAdvances[i] - cluster.Advance);
                }

                continue;
            }

            var unitAdvance = GetSharedSourceNaturalAdvance(sourceIndex, naturalAdvances, shapedClusters);
            advance += unitAdvance * glyphScales[i];
            if (i < sourceIndexes.Count - 1 && ShouldAddSharedInterUnitSpacing(sourceIndex, sourceIndexes[i + 1], shapedClusters))
            {
                advance += Math.Max(0f, stepAdvances[i] - unitAdvance);
            }
        }

        return advance;
    }

    private static float GetSharedSourceNaturalAdvance(
        int sourceIndex,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters)
    {
        if (TryGetSharedShapedCluster(shapedClusters, sourceIndex, out var cluster))
        {
            return cluster.StartSourceIndex == sourceIndex ? cluster.Advance : 0f;
        }

        return sourceIndex >= 0 && sourceIndex < naturalAdvances.Count ? naturalAdvances[sourceIndex] : 0f;
    }

    private static bool ShouldAddSharedInterUnitSpacing(
        int sourceIndex,
        int nextSourceIndex,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters)
    {
        if (!TryGetSharedShapedCluster(shapedClusters, sourceIndex, out var cluster))
        {
            return true;
        }

        return nextSourceIndex >= cluster.EndSourceIndex;
    }

    private static List<int> GetSharedTextLengthGapStepIndexes(
        IReadOnlyList<int> sourceIndexes,
        int start,
        int count,
        IReadOnlyList<SharedInlineSizeShapedCluster>? shapedClusters)
    {
        var gapIndexes = new List<int>();
        var end = start + count;
        for (var i = start; i < end - 1; i++)
        {
            if (ShouldAddSharedInterUnitSpacing(sourceIndexes[i], sourceIndexes[i + 1], shapedClusters))
            {
                gapIndexes.Add(i);
            }
        }

        return gapIndexes;
    }

    private static SharedInlineSizeRunPlacement[] CreateSharedLinePlacements(
        IReadOnlyList<int> sourceIndexes,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<float> stepAdvances,
        IReadOnlyList<float> glyphScales,
        IReadOnlyList<bool> scaleRunFromStart,
        float drawX,
        float drawY)
    {
        var placements = new SharedInlineSizeRunPlacement[sourceIndexes.Count];
        var currentX = drawX;
        var currentY = drawY;
        var inlineOffset = 0f;
        var scaleOriginX = drawX;

        for (var i = 0; i < sourceIndexes.Count; i++)
        {
            var sourceIndex = sourceIndexes[i];
            var codepoint = codepoints[sourceIndex];
            if (codepoint.X.HasValue)
            {
                currentX = codepoint.X.Value;
                scaleOriginX = currentX;
                inlineOffset = 0f;
            }

            if (codepoint.Y.HasValue)
            {
                currentY = codepoint.Y.Value;
                inlineOffset = 0f;
            }

            currentX += codepoint.Dx;
            currentY += codepoint.Dy;

            var glyphScale = glyphScales[i];
            var appliedAdvance = naturalAdvances[sourceIndex] * glyphScale;
            var compilerPlacement = new PositionedCodepointPlacement(
                new SKPoint(currentX, currentY),
                0f,
                glyphScale,
                scaleRunFromStart[i] ? scaleOriginX : currentX,
                inlineOffset);
            var layoutPlacement = new SvgTextCodepointPlacement(
                compilerPlacement.Point,
                compilerPlacement.RotationDegrees,
                compilerPlacement.ScaleX,
                compilerPlacement.ScaleOriginX,
                compilerPlacement.InlineOffset,
                appliedAdvance,
                sourceIndex,
                codepoint.X.HasValue || codepoint.Y.HasValue || codepoint.Dx != 0f || codepoint.Dy != 0f
                    ? SvgTextPlacementKind.Positioned
                    : SvgTextPlacementKind.Wrapped);

            placements[i] = new SharedInlineSizeRunPlacement(
                sourceIndex,
                compilerPlacement,
                layoutPlacement,
                naturalAdvances[sourceIndex],
                appliedAdvance);

            if (i < sourceIndexes.Count - 1)
            {
                var stepAdvance = stepAdvances[i];
                ApplyInlineAdvance(codepoint.StyleSource, ref currentX, ref currentY, stepAdvance);
                inlineOffset += stepAdvance;
            }
        }

        return placements;
    }

    private static List<SvgTextPositionedSpan> CreateSharedPositionedSpans(
        IReadOnlyList<SharedInlineSizeRunPlacement> placements,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<int> charStarts,
        SKRect geometryBounds,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKPoint baselineOrigin)
    {
        var spans = new List<SvgTextPositionedSpan>();
        var start = 0;
        while (TryGetNextSharedRenderedPlacementGroup(placements, codepoints, ref start, out var groupStart, out var groupEnd, out var styleSource))
        {
            var text = CreateSharedPlacementText(placements, codepoints, groupStart, groupEnd);
            var compilerPlacements = CreateSharedCompilerPlacements(placements, groupStart, groupEnd);
            var layoutPlacements = CreateSharedLayoutPlacements(placements, groupStart, groupEnd);
            var bounds = MeasureCodepointPlacementBounds(styleSource, text, compilerPlacements, viewport, assetLoader, out var advance);
            var firstSourceIndex = placements[groupStart].SourceCodepointIndex;
            var lastSourceIndex = placements[groupEnd - 1].SourceCodepointIndex;
            var utf16Start = charStarts[firstSourceIndex];
            var utf16End = charStarts[lastSourceIndex] + codepoints[lastSourceIndex].Codepoint.Length;
            var naturalAdvance = SumSharedNaturalAdvances(placements, naturalAdvances, groupStart, groupEnd);

            var textLengthSource = HasOwnTextLengthAdjustment(styleSource) ? styleSource : null;
            var appliedTextLength = textLengthSource is not null &&
                                    TryGetOwnTextLength(styleSource, viewport, IsVerticalWritingMode(styleSource), out var ownTextLength)
                ? ownTextLength
                : 0f;
            spans.Add(new SvgTextPositionedSpan(
                CreateSharedResolvedStyle(styleSource, geometryBounds, assetLoader),
                text,
                new SvgTextSourceRange(
                    styleSource,
                    new SvgTextIndexRange(utf16Start, utf16End - utf16Start),
                    new SvgTextIndexRange(firstSourceIndex, lastSourceIndex - firstSourceIndex + 1)),
                layoutPlacements,
                advance,
                bounds,
                textLengthSource,
                naturalAdvance,
                appliedTextLength,
                baselineOrigin));
        }

        return spans;
    }

    private static PositionedCodepointRun[] CreateSharedCompilerRuns(
        IReadOnlyList<SharedInlineSizeRunPlacement> placements,
        IReadOnlyList<FlattenedTextCodepoint> codepoints)
    {
        var runs = new List<PositionedCodepointRun>();
        var start = 0;
        while (TryGetNextSharedRenderedPlacementGroup(placements, codepoints, ref start, out var groupStart, out var groupEnd, out var styleSource))
        {
            var text = CreateSharedPlacementText(placements, codepoints, groupStart, groupEnd);
            var compilerPlacements = CreateSharedCompilerPlacements(placements, groupStart, groupEnd);
            runs.Add(new PositionedCodepointRun(styleSource, text, compilerPlacements));
        }

        return runs.ToArray();
    }

    private static bool TryGetNextSharedRenderedPlacementGroup(
        IReadOnlyList<SharedInlineSizeRunPlacement> placements,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        ref int start,
        out int groupStart,
        out int groupEnd,
        out SvgTextBase styleSource)
    {
        while (start < placements.Count &&
               !IsSharedRenderedCodepoint(codepoints[placements[start].SourceCodepointIndex]))
        {
            start++;
        }

        if (start >= placements.Count)
        {
            groupStart = 0;
            groupEnd = 0;
            styleSource = null!;
            return false;
        }

        groupStart = start;
        var previousSourceIndex = placements[start].SourceCodepointIndex;
        styleSource = codepoints[previousSourceIndex].StyleSource;
        start++;

        while (start < placements.Count)
        {
            var sourceIndex = placements[start].SourceCodepointIndex;
            if (!IsSharedRenderedCodepoint(codepoints[sourceIndex]) ||
                !ReferenceEquals(codepoints[sourceIndex].StyleSource, styleSource) ||
                sourceIndex != previousSourceIndex + 1)
            {
                break;
            }

            previousSourceIndex = sourceIndex;
            start++;
        }

        groupEnd = start;
        return groupEnd > groupStart;
    }

    private static string CreateSharedPlacementText(
        IReadOnlyList<SharedInlineSizeRunPlacement> placements,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        int start,
        int end)
    {
        var builder = new StringBuilder(end - start);
        for (var i = start; i < end; i++)
        {
            builder.Append(codepoints[placements[i].SourceCodepointIndex].Codepoint);
        }

        return builder.ToString();
    }

    private static PositionedCodepointPlacement[] CreateSharedCompilerPlacements(
        IReadOnlyList<SharedInlineSizeRunPlacement> placements,
        int start,
        int end)
    {
        var result = new PositionedCodepointPlacement[end - start];
        for (var i = start; i < end; i++)
        {
            result[i - start] = placements[i].CompilerPlacement;
        }

        return result;
    }

    private static SvgTextCodepointPlacement[] CreateSharedLayoutPlacements(
        IReadOnlyList<SharedInlineSizeRunPlacement> placements,
        int start,
        int end)
    {
        var result = new SvgTextCodepointPlacement[end - start];
        for (var i = start; i < end; i++)
        {
            result[i - start] = placements[i].LayoutPlacement;
        }

        return result;
    }

    private static float SumSharedNaturalAdvances(
        IReadOnlyList<SharedInlineSizeRunPlacement> placements,
        IReadOnlyList<float> naturalAdvances,
        int start,
        int end)
    {
        var advance = 0f;
        for (var i = start; i < end; i++)
        {
            advance += naturalAdvances[placements[i].SourceCodepointIndex];
        }

        return advance;
    }

    private static bool IsSharedRenderedCodepoint(FlattenedTextCodepoint codepoint)
    {
        return !IsInlineSizeBreakOpportunityWhitespace(codepoint.Codepoint) ||
               PreservesLineEdgeWhitespace(codepoint.StyleSource);
    }

    private static SvgTextResolvedStyle CreateSharedResolvedStyle(
        SvgTextBase styleSource,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(styleSource, geometryBounds, paint);
        var metrics = assetLoader.GetFontMetrics(paint);
        EnsureUsableFontMetrics(ref metrics, paint.TextSize);
        var lineBreakOptions = GetInlineSizeLineBreakOptions(styleSource);
        var spacing = new SvgTextSpacingStyle(
            ResolveSpacingValue(styleSource, styleSource.LetterSpacing, geometryBounds, paint.TextSize),
            ResolveSpacingValue(styleSource, styleSource.WordSpacing, geometryBounds, paint.TextSize),
            TryGetOwnTextLength(styleSource, geometryBounds, IsVerticalWritingMode(styleSource), out var textLength) ? textLength : 0f,
            GetOwnLengthAdjust(styleSource) == SvgTextLengthAdjust.SpacingAndGlyphs,
            GetOwnLengthAdjust(styleSource) == SvgTextLengthAdjust.Spacing);
        return new SvgTextResolvedStyle(
            styleSource,
            new SvgTextFontSelection(
                paint.Typeface?.FamilyName,
                paint.TextSize,
                paint.Typeface?.FontWeight ?? SKFontStyleWeight.Normal,
                paint.Typeface?.FontWidth ?? SKFontStyleWidth.Normal,
                paint.Typeface?.FontSlant ?? SKFontStyleSlant.Upright,
                paint.TextEncoding,
                paint.LcdRenderText,
                paint.SubpixelText,
                paint.Typeface),
            IsRightToLeft(styleSource) ? SvgTextDirection.RightToLeft : SvgTextDirection.LeftToRight,
            SvgTextBidiResolver.ResolveUnicodeBidi(styleSource),
            ToSharedLayoutFlow(GetInlineSizeFlow(styleSource)),
            styleSource.TextAnchor,
            styleSource.TextDecoration,
            new SvgTextLineBreakPolicy(
                GetInlineSizeWhiteSpace(styleSource),
                GetInlineSizeWhiteSpaceModel(styleSource),
                lineBreakOptions.OverflowWrapAnywhere,
                lineBreakOptions.WordBreakBreakAll,
                lineBreakOptions.WordBreakKeepAll,
                lineBreakOptions.LineBreakAnywhere,
                lineBreakOptions.LineBreakLoose,
                lineBreakOptions.StrictLineBreak),
            spacing,
            ResolveInlineSizeLineAdvance(styleSource, geometryBounds, assetLoader, paint, metrics),
            default,
            null,
            styleSource.TextOverflow,
            styleSource.ShapeInside,
            styleSource.ShapeSubtract);
    }

    private static SvgTextLayoutFlow ToSharedLayoutFlow(InlineSizeFlow flow)
    {
        return flow switch
        {
            InlineSizeFlow.HorizontalRightToLeft => SvgTextLayoutFlow.HorizontalRightToLeft,
            InlineSizeFlow.VerticalRightToLeftColumns => SvgTextLayoutFlow.VerticalRightToLeftColumns,
            InlineSizeFlow.VerticalLeftToRightColumns => SvgTextLayoutFlow.VerticalLeftToRightColumns,
            _ => SvgTextLayoutFlow.HorizontalLeftToRight
        };
    }

    private static int[] CreateFlattenedUtf16Starts(IReadOnlyList<FlattenedTextCodepoint> codepoints)
    {
        var starts = new int[codepoints.Count + 1];
        for (var i = 0; i < codepoints.Count; i++)
        {
            starts[i + 1] = starts[i] + codepoints[i].Codepoint.Length;
        }

        return starts;
    }

    private static int[] CreateSharedRenderedUtf16Starts(IReadOnlyList<FlattenedTextCodepoint> codepoints)
    {
        var starts = new int[codepoints.Count + 1];
        var renderedCharIndex = 0;
        for (var i = 0; i < codepoints.Count; i++)
        {
            starts[i] = renderedCharIndex;
            if (IsSharedRenderedCodepoint(codepoints[i]))
            {
                renderedCharIndex += codepoints[i].Codepoint.Length;
            }
        }

        starts[codepoints.Count] = renderedCharIndex;
        return starts;
    }

    private static void AppendSharedDomClusters(
        IReadOnlyList<PositionedCodepointRun> runs,
        SvgTextLayoutLine line,
        float lineStartOffset,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        List<SvgTextDomClusterMetric> domClusters,
        ref int domCharIndex)
    {
        var spanIndex = 0;
        for (var runIndex = 0; runIndex < runs.Count && spanIndex < line.PositionedSpans.Count; runIndex++, spanIndex++)
        {
            var run = runs[runIndex];
            var span = line.PositionedSpans[spanIndex];
            if (!IsVerticalWritingMode(run.StyleSource) &&
                TryCreatePlacedTextRunMetrics(
                    run.StyleSource,
                    run.Text,
                    viewport,
                    assetLoader,
                    run.Placements,
                    line.BaselineOrigin.X,
                    out var runClusters,
                    out _))
            {
                var localCharIndex = 0;
                for (var clusterIndex = 0; clusterIndex < runClusters.Length; clusterIndex++)
                {
                    var cluster = runClusters[clusterIndex];
                    if (cluster.CharLength <= 0)
                    {
                        continue;
                    }

                    domClusters.Add(new SvgTextDomClusterMetric(
                        span.SourceRange.Utf16Range.Start + localCharIndex,
                        cluster.CharLength,
                        lineStartOffset + cluster.StartOffset,
                        lineStartOffset + cluster.EndOffset,
                        cluster.StartPoint,
                        cluster.EndPoint,
                        cluster.Extent,
                        cluster.RotationDegrees)
                    {
                        HitExtent = cluster.HitExtent
                    });
                    localCharIndex += cluster.CharLength;
                }

                domCharIndex = Math.Max(domCharIndex, span.SourceRange.Utf16Range.Start + localCharIndex);

                continue;
            }

            AppendSharedFallbackDomClusters(run, span, lineStartOffset, viewport, assetLoader, domClusters, ref domCharIndex);
        }
    }

    private static void AppendSharedFallbackDomClusters(
        PositionedCodepointRun run,
        SvgTextPositionedSpan span,
        float lineStartOffset,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        List<SvgTextDomClusterMetric> domClusters,
        ref int domCharIndex)
    {
        var codepoints = SplitCodepointsReadOnly(run.Text);
        var inlineDirection = GetInlineAdvanceDirection(run.StyleSource);
        var localOffset = 0f;
        var localCharIndex = 0;
        for (var i = 0; i < codepoints.Count && i < run.Placements.Length; i++)
        {
            var placement = run.Placements[i];
            var advance = MeasureNaturalTextAdvance(run.StyleSource, codepoints[i], viewport, assetLoader) * placement.ScaleX;
            if (i + 1 < run.Placements.Length)
            {
                var next = run.Placements[i + 1].Point;
                var dx = next.X - placement.Point.X;
                var dy = next.Y - placement.Point.Y;
                var distance = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (distance > TextLengthTolerance)
                {
                    advance = distance;
                }
            }

            if (!IsValidPositiveAdvance(advance))
            {
                advance = 0f;
            }

            var endX = placement.Point.X;
            var endY = placement.Point.Y;
            ApplyInlineAdvance(run.StyleSource, ref endX, ref endY, advance);
            var endPoint = new SKPoint(endX, endY);
            var extent = MeasureCodepointPlacementBounds(
                run.StyleSource,
                codepoints[i],
                new[] { placement },
                viewport,
                assetLoader,
                out _);
            if (extent.IsEmpty && IsVerticalWritingMode(run.StyleSource))
            {
                extent = new SKRect(
                    placement.Point.X - 1f,
                    Math.Min(placement.Point.Y, placement.Point.Y + (advance * inlineDirection)),
                    placement.Point.X + 1f,
                    Math.Max(placement.Point.Y, placement.Point.Y + (advance * inlineDirection)));
            }

            domClusters.Add(new SvgTextDomClusterMetric(
                span.SourceRange.Utf16Range.Start + localCharIndex,
                codepoints[i].Length,
                lineStartOffset + localOffset,
                lineStartOffset + localOffset + advance,
                placement.Point,
                endPoint,
                extent,
                placement.RotationDegrees));
            localCharIndex += codepoints[i].Length;
            domCharIndex = Math.Max(domCharIndex, span.SourceRange.Utf16Range.Start + localCharIndex);
            localOffset += advance;
        }
    }

    private static float GetSharedLineFinalX(IReadOnlyList<PositionedCodepointRun> runs, float fallback)
    {
        if (runs.Count == 0)
        {
            return fallback;
        }

        var run = runs[runs.Count - 1];
        if (run.Placements.Length == 0)
        {
            return fallback;
        }

        var placement = run.Placements[run.Placements.Length - 1];
        MoveToAfterPositionedRun(run.StyleSource, placement.Point, Math.Max(0f, placement.InlineOffset), out var finalX, out _);
        return finalX;
    }

    private static float GetSharedLineFinalY(IReadOnlyList<PositionedCodepointRun> runs, float fallback)
    {
        if (runs.Count == 0)
        {
            return fallback;
        }

        var run = runs[runs.Count - 1];
        if (run.Placements.Length == 0)
        {
            return fallback;
        }

        var placement = run.Placements[run.Placements.Length - 1];
        MoveToAfterPositionedRun(run.StyleSource, placement.Point, Math.Max(0f, placement.InlineOffset), out _, out var finalY);
        return finalY;
    }
}
