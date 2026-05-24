#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShimSkiaSharp;
using Svg;

namespace Svg.Skia;

internal readonly record struct SvgTextLayoutStyle(
    SvgTextDirection Direction,
    SvgUnicodeBidiMode UnicodeBidi,
    SvgWhiteSpace WhiteSpace,
    SvgTextWhiteSpaceModel WhiteSpaceModel,
    SvgTextLineBreakOptions LineBreakOptions)
{
    public SvgTextLayoutStyle(
        SvgTextDirection direction,
        SvgUnicodeBidiMode unicodeBidi,
        SvgWhiteSpace whiteSpace,
        SvgTextLineBreakOptions lineBreakOptions)
        : this(
            direction,
            unicodeBidi,
            whiteSpace,
            SvgTextWhiteSpaceModel.FromLegacy(whiteSpace),
            lineBreakOptions)
    {
    }

    public static SvgTextLayoutStyle Default { get; } = new(
        SvgTextDirection.LeftToRight,
        SvgUnicodeBidiMode.Normal,
        SvgWhiteSpace.Normal,
        SvgTextWhiteSpaceModel.Normal,
        new SvgTextLineBreakOptions(
            OverflowWrapAnywhere: false,
            WordBreakBreakAll: false,
            WordBreakKeepAll: false,
            LineBreakAnywhere: false,
            LineBreakLoose: false,
            StrictLineBreak: false));

    public bool AllowsSoftWrapping => WhiteSpaceModel.AllowsSoftWrapping;

    public bool PreservesLineEdgeWhitespace => WhiteSpaceModel.PreservesLineEdgeWhitespace;
}

internal readonly record struct SvgTextLayoutInputRun(
    int SourceRunIndex,
    string Text,
    SvgTextLayoutStyle Style);

internal readonly record struct SvgTextLayoutCodepoint(
    int CodepointIndex,
    string Text,
    int Scalar,
    int StartCharIndex,
    int CharLength,
    int SourceRunIndex,
    int ClusterIndex,
    SvgTextLayoutStyle Style);

internal readonly record struct SvgTextCluster(
    int ClusterIndex,
    int StartCodepointIndex,
    int CodepointCount,
    int StartCharIndex,
    int Length,
    int SourceRunIndex,
    string Text);

internal readonly record struct SvgTextLayoutResolvedRun(
    int RunIndex,
    int StartClusterIndex,
    int ClusterCount,
    int StartCharIndex,
    int Length,
    SvgTextDirection Direction,
    int BidiLevel,
    int[] SourceRunIndexes,
    string Text);

internal sealed class SvgTextLayoutPlan
{
    public SvgTextLayoutPlan(
        string text,
        SvgTextLayoutStyle paragraphStyle,
        SvgTextLayoutCodepoint[] codepoints,
        SvgTextCluster[] clusters,
        SvgTextLayoutResolvedRun[] logicalRuns,
        SvgTextLayoutResolvedRun[] visualRuns,
        SvgTextBreakOpportunity[] breakOpportunities,
        bool hasVisualReordering)
    {
        Text = text;
        ParagraphStyle = paragraphStyle;
        Codepoints = codepoints;
        Clusters = clusters;
        LogicalRuns = logicalRuns;
        VisualRuns = visualRuns;
        BreakOpportunities = breakOpportunities;
        HasVisualReordering = hasVisualReordering;
    }

    public string Text { get; }

    public SvgTextLayoutStyle ParagraphStyle { get; }

    public IReadOnlyList<SvgTextLayoutCodepoint> Codepoints { get; }

    public IReadOnlyList<SvgTextCluster> Clusters { get; }

    public IReadOnlyList<SvgTextLayoutResolvedRun> LogicalRuns { get; }

    public IReadOnlyList<SvgTextLayoutResolvedRun> VisualRuns { get; }

    public IReadOnlyList<SvgTextBreakOpportunity> BreakOpportunities { get; }

    public bool HasVisualReordering { get; }

    public IReadOnlyList<SvgTextLayoutResolvedRun> CreateVisualRunsForLine(int startCharIndex, int length)
    {
        return SvgTextLayoutPlanner.CreateVisualRunsForLine(this, startCharIndex, length);
    }
}

internal static class SvgTextLayoutPlanner
{
    private readonly record struct WrappedClusterSegment(
        int StartClusterIndex,
        int ClusterCount,
        float Advance,
        bool ForcesLineBreak);

    private readonly record struct WrappedClusterLine(
        int LineIndex,
        int StartClusterIndex,
        int ClusterCount,
        float NaturalAdvance,
        bool Overflows);

    public static SvgTextLayoutPlan Create(
        string text,
        SvgTextDirection direction,
        SvgUnicodeBidiMode unicodeBidi,
        SvgWhiteSpace whiteSpace,
        SvgTextLineBreakOptions lineBreakOptions)
    {
        var style = new SvgTextLayoutStyle(
            direction,
            unicodeBidi,
            whiteSpace,
            SvgTextWhiteSpaceModel.FromLegacy(whiteSpace),
            lineBreakOptions);
        return Create([new SvgTextLayoutInputRun(0, text, style)], style);
    }

    public static SvgTextLayoutPlan Create(
        IReadOnlyList<SvgTextLayoutInputRun> runs,
        SvgTextLayoutStyle paragraphStyle)
    {
        if (runs.Count == 0)
        {
            return new SvgTextLayoutPlan(
                string.Empty,
                paragraphStyle,
                Array.Empty<SvgTextLayoutCodepoint>(),
                Array.Empty<SvgTextCluster>(),
                Array.Empty<SvgTextLayoutResolvedRun>(),
                Array.Empty<SvgTextLayoutResolvedRun>(),
                Array.Empty<SvgTextBreakOpportunity>(),
                hasVisualReordering: false);
        }

        var codepoints = CreateCodepoints(runs, out var text);
        var clusters = CreateClusters(codepoints, text);
        ApplyClusterIndexes(codepoints, clusters);

        var logicalRuns = CreateResolvedRuns(text, codepoints, clusters, paragraphStyle, visualOrder: false);
        var visualRuns = CreateResolvedRuns(text, codepoints, clusters, paragraphStyle, visualOrder: true);
        var breakOpportunities = CreateBreakOpportunities(codepoints);
        var hasVisualReordering = HasVisualReordering(logicalRuns, visualRuns);
        var immutableCodepoints = new SvgTextLayoutCodepoint[codepoints.Count];
        for (var i = 0; i < codepoints.Count; i++)
        {
            immutableCodepoints[i] = codepoints[i].ToImmutable();
        }

        return new SvgTextLayoutPlan(
            text,
            paragraphStyle,
            immutableCodepoints,
            clusters,
            logicalRuns,
            visualRuns,
            breakOpportunities,
            hasVisualReordering);
    }

    public static IReadOnlyList<SvgTextLayoutResolvedRun> CreateVisualRunsForLine(
        SvgTextLayoutPlan plan,
        int startCharIndex,
        int length)
    {
        if (length <= 0 || plan.LogicalRuns.Count == 0)
        {
            return Array.Empty<SvgTextLayoutResolvedRun>();
        }

        var paragraphRuns = plan.LogicalRuns
            .Select(static run => new SvgTextBidiRun(run.StartCharIndex, run.Length, run.Direction, run.BidiLevel))
            .ToArray();
        var lineBidiRuns = SvgTextBidiResolver.CreateLineVisualRuns(
            paragraphRuns,
            startCharIndex,
            length,
            plan.ParagraphStyle.Direction);
        if (lineBidiRuns.Count == 0)
        {
            return Array.Empty<SvgTextLayoutResolvedRun>();
        }

        var result = new List<SvgTextLayoutResolvedRun>(lineBidiRuns.Count);
        for (var i = 0; i < lineBidiRuns.Count; i++)
        {
            var lineRun = lineBidiRuns[i];
            var runClusters = GetRunClusters(lineRun, plan.Clusters, assignedVisualClusters: null);
            if (runClusters.Count == 0)
            {
                continue;
            }

            result.Add(CreateResolvedRun(
                result.Count,
                plan.Text,
                plan.Codepoints,
                runClusters,
                lineRun.Direction,
                lineRun.Level));
        }

        return MergeAdjacentCompatibleRuns(result);
    }

    public static SvgTextLayoutResolvedRun[] CreateLineScopedVisualRuns(
        SvgTextLayoutPlan plan,
        int startCharIndex,
        int length)
    {
        if (plan.Codepoints.Count == 0 || length <= 0)
        {
            return Array.Empty<SvgTextLayoutResolvedRun>();
        }

        var endCharIndex = startCharIndex + length;
        var inputRuns = new List<SvgTextLayoutInputRun>();
        var firstClusterIndex = -1;
        for (var i = 0; i < plan.Codepoints.Count; i++)
        {
            var codepoint = plan.Codepoints[i];
            var codepointEnd = codepoint.StartCharIndex + codepoint.CharLength;
            if (codepoint.StartCharIndex < startCharIndex || codepointEnd > endCharIndex)
            {
                continue;
            }

            if (firstClusterIndex < 0)
            {
                firstClusterIndex = codepoint.ClusterIndex;
            }

            inputRuns.Add(new SvgTextLayoutInputRun(codepoint.SourceRunIndex, codepoint.Text, codepoint.Style));
        }

        if (inputRuns.Count == 0)
        {
            return Array.Empty<SvgTextLayoutResolvedRun>();
        }

        var linePlan = Create(inputRuns, plan.ParagraphStyle);
        var visualRuns = new SvgTextLayoutResolvedRun[linePlan.VisualRuns.Count];
        for (var i = 0; i < linePlan.VisualRuns.Count; i++)
        {
            var visualRun = linePlan.VisualRuns[i];
            visualRuns[i] = visualRun with
            {
                RunIndex = i,
                StartClusterIndex = firstClusterIndex + visualRun.StartClusterIndex,
                StartCharIndex = startCharIndex + visualRun.StartCharIndex
            };
        }

        return visualRuns;
    }

    public static SvgTextWrappedLayoutResult CreateWrappedLayout(
        SvgTextLayoutPlan plan,
        IReadOnlyList<float> clusterAdvances,
        SvgTextWrappedLayoutOptions options)
    {
        if (plan is null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (plan.Clusters.Count == 0 ||
            options.EffectiveInlineSize <= 0f)
        {
            return new SvgTextWrappedLayoutResult(
                Array.Empty<SvgTextWrappedLayoutLine>(),
                SvgTextDomMetrics.Empty,
                SKRect.Empty,
                0f);
        }

        var advances = CreateClusterAdvances(plan.Clusters.Count, clusterAdvances);
        var segments = CreateWrappedClusterSegments(plan, advances);
        var lines = CreateWrappedClusterLines(plan, segments, options);
        if (lines.Count == 0)
        {
            return new SvgTextWrappedLayoutResult(
                Array.Empty<SvgTextWrappedLayoutLine>(),
                SvgTextDomMetrics.Empty,
                SKRect.Empty,
                0f);
        }

        var targetAdvances = CreateWrappedTextLengthTargets(lines, options);
        var inlineProgression = GetWrappedInlineProgression(plan, options.Flow);
        var blockProgression = GetWrappedBlockProgression(options.Flow);
        var layoutLines = new List<SvgTextWrappedLayoutLine>(lines.Count);
        var domClusters = new List<SvgTextDomClusterMetric>(plan.Clusters.Count);
        var bounds = SKRect.Empty;
        var computedTextLength = 0f;
        var domCharCount = 0;

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineTargetAdvance = lineIndex < targetAdvances.Length
                ? targetAdvances[lineIndex]
                : line.NaturalAdvance;
            var visualRuns = CreateLineScopedVisualRuns(
                plan,
                plan.Clusters[line.StartClusterIndex].StartCharIndex,
                GetWrappedLineCharLength(plan, line));
            var visualClusterIndexes = CreateWrappedVisualClusterOrder(plan, line, visualRuns);
            var baselineOrigin = GetWrappedLineOrigin(options, line.LineIndex, blockProgression, inlineProgression);
            var placements = CreateWrappedClusterPlacements(
                plan,
                visualClusterIndexes,
                advances,
                options,
                baselineOrigin,
                inlineProgression,
                line.NaturalAdvance,
                lineTargetAdvance,
                line.Overflows,
                out var overflowMarker,
                out var renderedAdvance,
                out var lineBounds);

            layoutLines.Add(new SvgTextWrappedLayoutLine(
                line.LineIndex,
                options.Flow,
                baselineOrigin,
                options.EffectiveInlineSize,
                line.NaturalAdvance,
                renderedAdvance,
                inlineProgression,
                placements,
                visualRuns,
                overflowMarker,
                lineBounds));
            UnionWrappedBounds(ref bounds, lineBounds);
            AppendWrappedDomClusters(placements, options.Flow, inlineProgression, lineStartOffset: computedTextLength, domClusters, ref domCharCount);
            computedTextLength += options.HasTextLength ? lineTargetAdvance : line.NaturalAdvance;
        }

        domClusters.Sort(static (left, right) => left.StartCharIndex != right.StartCharIndex
            ? left.StartCharIndex.CompareTo(right.StartCharIndex)
            : left.CharLength.CompareTo(right.CharLength));

        return new SvgTextWrappedLayoutResult(
            layoutLines,
            new SvgTextDomMetrics(domClusters, domCharCount, computedTextLength),
            bounds,
            computedTextLength);
    }

    private static float[] CreateClusterAdvances(int clusterCount, IReadOnlyList<float> clusterAdvances)
    {
        var advances = new float[clusterCount];
        for (var i = 0; i < advances.Length; i++)
        {
            advances[i] = i < clusterAdvances.Count
                ? Math.Max(0f, clusterAdvances[i])
                : 0f;
        }

        return advances;
    }

    private static List<WrappedClusterSegment> CreateWrappedClusterSegments(
        SvgTextLayoutPlan plan,
        IReadOnlyList<float> advances)
    {
        var segments = new List<WrappedClusterSegment>();
        var segmentStart = 0;
        var segmentAdvance = 0f;

        void FlushSegment(int endClusterIndex, bool forcesLineBreak)
        {
            if (endClusterIndex >= segmentStart)
            {
                segments.Add(new WrappedClusterSegment(
                    segmentStart,
                    endClusterIndex - segmentStart + 1,
                    segmentAdvance,
                    forcesLineBreak));
            }
            else if (forcesLineBreak)
            {
                segments.Add(new WrappedClusterSegment(segmentStart, 0, 0f, true));
            }

            segmentStart = endClusterIndex + 1;
            segmentAdvance = 0f;
        }

        for (var clusterIndex = 0; clusterIndex < plan.Clusters.Count; clusterIndex++)
        {
            segmentAdvance += advances[clusterIndex];
            if (ForcesWrappedBreakAfterCluster(plan, clusterIndex))
            {
                FlushSegment(clusterIndex, forcesLineBreak: true);
                continue;
            }

            if (AllowsWrappedBreakAfterCluster(plan, clusterIndex))
            {
                FlushSegment(clusterIndex, forcesLineBreak: false);
            }
        }

        if (segmentStart < plan.Clusters.Count)
        {
            FlushSegment(plan.Clusters.Count - 1, forcesLineBreak: false);
        }

        return segments;
    }

    private static List<WrappedClusterLine> CreateWrappedClusterLines(
        SvgTextLayoutPlan plan,
        IReadOnlyList<WrappedClusterSegment> segments,
        SvgTextWrappedLayoutOptions options)
    {
        var lines = new List<WrappedClusterLine>();
        var maxLineCount = Math.Min(
            Math.Max(1, options.Wrapping.EffectiveMaxLineSearchCount),
            Math.Max(1, plan.Clusters.Count + 1));
        var lineStart = -1;
        var lineClusterCount = 0;
        var lineAdvance = 0f;

        void FlushLine(bool overflows)
        {
            if (lineStart < 0 || lineClusterCount <= 0)
            {
                lineStart = -1;
                lineClusterCount = 0;
                lineAdvance = 0f;
                return;
            }

            lines.Add(new WrappedClusterLine(lines.Count, lineStart, lineClusterCount, lineAdvance, overflows));
            lineStart = -1;
            lineClusterCount = 0;
            lineAdvance = 0f;
        }

        for (var segmentIndex = 0; segmentIndex < segments.Count && lines.Count < maxLineCount; segmentIndex++)
        {
            var segment = segments[segmentIndex];
            if (segment.ClusterCount == 0)
            {
                FlushLine(overflows: false);
                continue;
            }

            if (lineStart < 0)
            {
                lineStart = segment.StartClusterIndex;
            }

            var candidateAdvance = lineAdvance + segment.Advance;
            if (lineClusterCount > 0 &&
                candidateAdvance > options.EffectiveInlineSize + options.Wrapping.EffectiveTextLengthTolerance)
            {
                FlushLine(overflows: false);
                if (lines.Count >= maxLineCount)
                {
                    break;
                }

                lineStart = segment.StartClusterIndex;
                candidateAdvance = segment.Advance;
            }

            lineClusterCount += segment.ClusterCount;
            lineAdvance = candidateAdvance;

            if (segment.ForcesLineBreak)
            {
                FlushLine(overflows: false);
                continue;
            }

            if (lineClusterCount == segment.ClusterCount &&
                segment.Advance > options.EffectiveInlineSize + options.Wrapping.EffectiveTextLengthTolerance)
            {
                FlushLine(overflows: true);
            }
        }

        if (lines.Count < maxLineCount)
        {
            FlushLine(lineAdvance > options.EffectiveInlineSize + options.Wrapping.EffectiveTextLengthTolerance);
        }

        return lines;
    }

    private static float[] CreateWrappedTextLengthTargets(
        IReadOnlyList<WrappedClusterLine> lines,
        SvgTextWrappedLayoutOptions options)
    {
        var targets = new float[lines.Count];
        if (!options.HasTextLength || lines.Count == 0)
        {
            for (var i = 0; i < targets.Length; i++)
            {
                targets[i] = lines[i].NaturalAdvance;
            }

            return targets;
        }

        var totalNaturalAdvance = 0f;
        for (var i = 0; i < lines.Count; i++)
        {
            totalNaturalAdvance += Math.Max(0f, lines[i].NaturalAdvance);
        }

        if (totalNaturalAdvance <= options.Wrapping.EffectiveTextLengthTolerance)
        {
            var equalTarget = options.TextLength / lines.Count;
            for (var i = 0; i < targets.Length; i++)
            {
                targets[i] = equalTarget;
            }

            return targets;
        }

        var assigned = 0f;
        var lastNonEmptyLine = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].NaturalAdvance <= 0f)
            {
                targets[i] = 0f;
                continue;
            }

            lastNonEmptyLine = i;
            targets[i] = options.TextLength * lines[i].NaturalAdvance / totalNaturalAdvance;
            assigned += targets[i];
        }

        if (lastNonEmptyLine >= 0)
        {
            targets[lastNonEmptyLine] += options.TextLength - assigned;
        }

        return targets;
    }

    private static SvgTextWrappedClusterPlacement[] CreateWrappedClusterPlacements(
        SvgTextLayoutPlan plan,
        IReadOnlyList<int> visualClusterIndexes,
        IReadOnlyList<float> advances,
        SvgTextWrappedLayoutOptions options,
        SKPoint baselineOrigin,
        int inlineProgression,
        float naturalAdvance,
        float targetAdvance,
        bool overflows,
        out SvgTextOverflowMarkerPlacement? overflowMarker,
        out float renderedAdvance,
        out SKRect bounds)
    {
        overflowMarker = null;
        bounds = SKRect.Empty;
        var placements = new List<SvgTextWrappedClusterPlacement>(visualClusterIndexes.Count);
        var visibleClusterCount = visualClusterIndexes.Count;
        var markerAdvance = options.HasOverflowMarker && overflows
            ? Math.Max(0f, options.OverflowMarkerAdvance)
            : 0f;
        var availableAdvance = markerAdvance > 0f
            ? Math.Max(0f, options.EffectiveInlineSize - markerAdvance)
            : float.PositiveInfinity;
        if (!float.IsPositiveInfinity(availableAdvance))
        {
            visibleClusterCount = 0;
            var used = 0f;
            for (var i = 0; i < visualClusterIndexes.Count; i++)
            {
                var clusterAdvance = advances[visualClusterIndexes[i]];
                if (used + clusterAdvance > availableAdvance + options.Wrapping.EffectiveTextLengthTolerance)
                {
                    break;
                }

                used += clusterAdvance;
                visibleClusterCount++;
            }
        }

        var applyTextLength = options.HasTextLength &&
                              naturalAdvance > options.Wrapping.EffectiveTextLengthTolerance &&
                              Math.Abs(targetAdvance - naturalAdvance) > options.Wrapping.EffectiveTextLengthTolerance &&
                              visibleClusterCount > 0;
        var scale = applyTextLength && options.LengthAdjust == SvgTextLengthAdjust.SpacingAndGlyphs
            ? targetAdvance / naturalAdvance
            : 1f;
        var extraGap = applyTextLength &&
                       options.LengthAdjust == SvgTextLengthAdjust.Spacing &&
                       visibleClusterCount > 1
            ? (targetAdvance - naturalAdvance) / (visibleClusterCount - 1)
            : 0f;
        var inlineOffset = 0f;

        for (var i = 0; i < visibleClusterCount; i++)
        {
            var clusterIndex = visualClusterIndexes[i];
            var cluster = plan.Clusters[clusterIndex];
            var point = GetWrappedInlinePoint(options.Flow, baselineOrigin, inlineProgression, inlineOffset);
            var advance = Math.Max(0f, advances[clusterIndex] * scale);
            var placement = new SvgTextWrappedClusterPlacement(
                clusterIndex,
                new SvgTextIndexRange(cluster.StartCharIndex, cluster.Length),
                cluster.Text,
                ResolveClusterDirection(plan, clusterIndex),
                point,
                inlineOffset,
                advance,
                scale);
            placements.Add(placement);
            UnionWrappedBounds(ref bounds, CreateWrappedInlineBounds(options.Flow, point, advance, inlineProgression));
            inlineOffset += advance;
            if (i + 1 < visibleClusterCount)
            {
                inlineOffset = Math.Max(0f, inlineOffset + extraGap);
            }
        }

        if (markerAdvance > 0f && options.OverflowMarker is not null)
        {
            var markerPoint = GetWrappedInlinePoint(options.Flow, baselineOrigin, inlineProgression, inlineOffset);
            overflowMarker = new SvgTextOverflowMarkerPlacement(
                options.OverflowMarker,
                markerPoint,
                inlineOffset,
                markerAdvance);
            UnionWrappedBounds(ref bounds, CreateWrappedInlineBounds(options.Flow, markerPoint, markerAdvance, inlineProgression));
            inlineOffset += markerAdvance;
        }

        renderedAdvance = options.HasTextLength && markerAdvance <= 0f
            ? Math.Max(0f, targetAdvance)
            : Math.Max(0f, inlineOffset);
        return placements.ToArray();
    }

    private static int[] CreateWrappedVisualClusterOrder(
        SvgTextLayoutPlan plan,
        WrappedClusterLine line,
        IReadOnlyList<SvgTextLayoutResolvedRun> visualRuns)
    {
        var lineStart = line.StartClusterIndex;
        var lineEnd = line.StartClusterIndex + line.ClusterCount;
        var result = new List<int>(line.ClusterCount);
        var assigned = new HashSet<int>();

        for (var runIndex = 0; runIndex < visualRuns.Count; runIndex++)
        {
            var run = visualRuns[runIndex];
            var runClusters = new List<int>();
            for (var clusterIndex = lineStart; clusterIndex < lineEnd; clusterIndex++)
            {
                var cluster = plan.Clusters[clusterIndex];
                if (!RangeIntersects(
                        cluster.StartCharIndex,
                        cluster.StartCharIndex + cluster.Length,
                        run.StartCharIndex,
                        run.StartCharIndex + run.Length))
                {
                    continue;
                }

                runClusters.Add(clusterIndex);
            }

            if (run.Direction == SvgTextDirection.RightToLeft)
            {
                runClusters.Reverse();
            }

            for (var i = 0; i < runClusters.Count; i++)
            {
                if (assigned.Add(runClusters[i]))
                {
                    result.Add(runClusters[i]);
                }
            }
        }

        for (var clusterIndex = lineStart; clusterIndex < lineEnd; clusterIndex++)
        {
            if (assigned.Add(clusterIndex))
            {
                result.Add(clusterIndex);
            }
        }

        return result.ToArray();
    }

    private static void AppendWrappedDomClusters(
        IReadOnlyList<SvgTextWrappedClusterPlacement> placements,
        SvgTextLayoutFlow flow,
        int inlineProgression,
        float lineStartOffset,
        List<SvgTextDomClusterMetric> domClusters,
        ref int domCharCount)
    {
        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var endPoint = GetWrappedInlinePoint(flow, placement.Point, inlineProgression, placement.Advance);

            domClusters.Add(new SvgTextDomClusterMetric(
                placement.Utf16Range.Start,
                placement.Utf16Range.Length,
                lineStartOffset + placement.InlineOffset,
                lineStartOffset + placement.InlineOffset + placement.Advance,
                placement.Point,
                endPoint,
                CreateWrappedInlineBounds(flow, placement.Point, placement.Advance, inlineProgression),
                0f));
            domCharCount = Math.Max(domCharCount, placement.Utf16Range.End);
        }
    }

    private static bool AllowsWrappedBreakAfterCluster(SvgTextLayoutPlan plan, int clusterIndex)
    {
        return HasWrappedBreakAfterCluster(plan, clusterIndex, includeForced: false);
    }

    private static bool ForcesWrappedBreakAfterCluster(SvgTextLayoutPlan plan, int clusterIndex)
    {
        return plan.Clusters[clusterIndex].Text.IndexOf('\n') >= 0 ||
               HasWrappedBreakAfterCluster(plan, clusterIndex, includeForced: true, forcedOnly: true);
    }

    private static bool HasWrappedBreakAfterCluster(
        SvgTextLayoutPlan plan,
        int clusterIndex,
        bool includeForced,
        bool forcedOnly = false)
    {
        var cluster = plan.Clusters[clusterIndex];
        var clusterLastCodepointIndex = cluster.StartCodepointIndex + cluster.CodepointCount - 1;
        for (var i = 0; i < plan.BreakOpportunities.Count; i++)
        {
            var opportunity = plan.BreakOpportunities[i];
            if (!includeForced && opportunity.Kind == SvgTextBreakOpportunityKind.ForcedLine)
            {
                continue;
            }

            if (forcedOnly && opportunity.Kind != SvgTextBreakOpportunityKind.ForcedLine)
            {
                continue;
            }

            if (opportunity.BeforeCodepointIndex <= clusterLastCodepointIndex &&
                opportunity.AfterCodepointIndex > clusterLastCodepointIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetWrappedLineCharLength(SvgTextLayoutPlan plan, WrappedClusterLine line)
    {
        var first = plan.Clusters[line.StartClusterIndex];
        var last = plan.Clusters[line.StartClusterIndex + line.ClusterCount - 1];
        return last.StartCharIndex + last.Length - first.StartCharIndex;
    }

    private static SvgTextDirection ResolveClusterDirection(SvgTextLayoutPlan plan, int clusterIndex)
    {
        var cluster = plan.Clusters[clusterIndex];
        for (var i = 0; i < plan.LogicalRuns.Count; i++)
        {
            var run = plan.LogicalRuns[i];
            if (RangeIntersects(
                    cluster.StartCharIndex,
                    cluster.StartCharIndex + cluster.Length,
                    run.StartCharIndex,
                    run.StartCharIndex + run.Length))
            {
                return run.Direction;
            }
        }

        return plan.ParagraphStyle.Direction;
    }

    private static int GetWrappedInlineProgression(SvgTextLayoutPlan plan, SvgTextLayoutFlow flow)
    {
        return flow switch
        {
            SvgTextLayoutFlow.HorizontalRightToLeft => -1,
            SvgTextLayoutFlow.VerticalLeftToRightColumns or SvgTextLayoutFlow.VerticalRightToLeftColumns =>
                plan.ParagraphStyle.Direction == SvgTextDirection.RightToLeft ? -1 : 1,
            _ => 1
        };
    }

    private static int GetWrappedBlockProgression(SvgTextLayoutFlow flow)
    {
        return flow == SvgTextLayoutFlow.VerticalRightToLeftColumns ? -1 : 1;
    }

    private static SKPoint GetWrappedLineOrigin(
        SvgTextWrappedLayoutOptions options,
        int lineIndex,
        int blockProgression,
        int inlineProgression)
    {
        if (options.Flow is SvgTextLayoutFlow.VerticalLeftToRightColumns or SvgTextLayoutFlow.VerticalRightToLeftColumns)
        {
            var x = options.Origin.X + (lineIndex * options.EffectiveLineAdvance * blockProgression);
            var y = inlineProgression < 0
                ? options.Origin.Y + options.EffectiveInlineSize
                : options.Origin.Y;
            return new SKPoint(x, y);
        }

        var originX = inlineProgression < 0
            ? options.Origin.X + options.EffectiveInlineSize
            : options.Origin.X;
        var originY = options.Origin.Y + (lineIndex * options.EffectiveLineAdvance);
        return new SKPoint(originX, originY);
    }

    private static SKPoint GetWrappedInlinePoint(
        SvgTextLayoutFlow flow,
        SKPoint origin,
        int inlineProgression,
        float inlineOffset)
    {
        return flow is SvgTextLayoutFlow.VerticalLeftToRightColumns or SvgTextLayoutFlow.VerticalRightToLeftColumns
            ? new SKPoint(origin.X, origin.Y + (inlineOffset * inlineProgression))
            : new SKPoint(origin.X + (inlineOffset * inlineProgression), origin.Y);
    }

    private static SKRect CreateWrappedInlineBounds(
        SvgTextLayoutFlow flow,
        SKPoint point,
        float advance,
        int inlineProgression)
    {
        if (flow is SvgTextLayoutFlow.VerticalLeftToRightColumns or SvgTextLayoutFlow.VerticalRightToLeftColumns)
        {
            var endY = point.Y + (advance * inlineProgression);
            return new SKRect(
                point.X - 0.5f,
                Math.Min(point.Y, endY),
                point.X + 0.5f,
                Math.Max(point.Y, endY));
        }

        var endX = point.X + (advance * inlineProgression);
        return new SKRect(
            Math.Min(point.X, endX),
            point.Y - 0.5f,
            Math.Max(point.X, endX),
            point.Y + 0.5f);
    }

    private static void UnionWrappedBounds(ref SKRect bounds, SKRect next)
    {
        if (next.IsEmpty)
        {
            return;
        }

        if (bounds.IsEmpty)
        {
            bounds = next;
            return;
        }

        bounds = new SKRect(
            Math.Min(bounds.Left, next.Left),
            Math.Min(bounds.Top, next.Top),
            Math.Max(bounds.Right, next.Right),
            Math.Max(bounds.Bottom, next.Bottom));
    }

    private static bool RangeIntersects(int firstStart, int firstEnd, int secondStart, int secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private sealed class MutableCodepoint
    {
        public MutableCodepoint(
            int codepointIndex,
            string text,
            int scalar,
            int startCharIndex,
            int charLength,
            int sourceRunIndex,
            SvgTextLayoutStyle style)
        {
            CodepointIndex = codepointIndex;
            Text = text;
            Scalar = scalar;
            StartCharIndex = startCharIndex;
            CharLength = charLength;
            SourceRunIndex = sourceRunIndex;
            Style = style;
        }

        public int CodepointIndex { get; }

        public string Text { get; }

        public int Scalar { get; }

        public int StartCharIndex { get; }

        public int CharLength { get; }

        public int SourceRunIndex { get; }

        public int ClusterIndex { get; set; } = -1;

        public SvgTextLayoutStyle Style { get; }

        public SvgTextLayoutCodepoint ToImmutable() => new(
            CodepointIndex,
            Text,
            Scalar,
            StartCharIndex,
            CharLength,
            SourceRunIndex,
            ClusterIndex,
            Style);
    }

    private static List<MutableCodepoint> CreateCodepoints(IReadOnlyList<SvgTextLayoutInputRun> runs, out string text)
    {
        var codepoints = new List<MutableCodepoint>();
        var builder = new StringBuilder();
        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var run = runs[runIndex];
            var runText = run.Text ?? string.Empty;
            var charIndex = 0;
            while (charIndex < runText.Length)
            {
                var localCharIndex = charIndex;
                var scalar = char.ConvertToUtf32(runText, charIndex);
                charIndex += scalar > 0xFFFF ? 2 : 1;

                var codepointText = char.ConvertFromUtf32(scalar);
                codepoints.Add(new MutableCodepoint(
                    codepoints.Count,
                    codepointText,
                    scalar,
                    builder.Length + localCharIndex,
                    codepointText.Length,
                    run.SourceRunIndex,
                    run.Style));
            }

            builder.Append(runText);
        }

        text = builder.ToString();
        return codepoints;
    }

    private static SvgTextCluster[] CreateClusters(IReadOnlyList<MutableCodepoint> codepoints, string text)
    {
        if (codepoints.Count == 0)
        {
            return Array.Empty<SvgTextCluster>();
        }

        var resolver = SvgDefaultTextBoundaryResolver.Instance;
        var clusterStartCharIndexes = new HashSet<int>(resolver.GetGraphemeClusterStartCharIndexes(text));
        var codepointTexts = codepoints.Select(static codepoint => codepoint.Text).ToArray();
        var clusters = new List<SvgTextCluster>();
        var clusterStart = 0;

        for (var i = 1; i < codepoints.Count; i++)
        {
            var startsNewCluster = clusterStartCharIndexes.Contains(codepoints[i].StartCharIndex) &&
                                   resolver.IsGraphemeClusterBoundary(codepointTexts, i - 1, i);
            if (startsNewCluster)
            {
                clusters.Add(CreateCluster(clusters.Count, clusterStart, i - 1, codepoints, text));
                clusterStart = i;
            }
        }

        clusters.Add(CreateCluster(clusters.Count, clusterStart, codepoints.Count - 1, codepoints, text));
        return clusters.ToArray();
    }

    private static SvgTextCluster CreateCluster(
        int clusterIndex,
        int startCodepointIndex,
        int endCodepointIndex,
        IReadOnlyList<MutableCodepoint> codepoints,
        string text)
    {
        var first = codepoints[startCodepointIndex];
        var last = codepoints[endCodepointIndex];
        var startCharIndex = first.StartCharIndex;
        var endCharIndex = last.StartCharIndex + last.CharLength;
        var sourceRunIndex = first.SourceRunIndex;
        for (var i = startCodepointIndex + 1; i <= endCodepointIndex; i++)
        {
            if (codepoints[i].SourceRunIndex != sourceRunIndex)
            {
                sourceRunIndex = -1;
                break;
            }
        }

        return new SvgTextCluster(
            clusterIndex,
            startCodepointIndex,
            endCodepointIndex - startCodepointIndex + 1,
            startCharIndex,
            endCharIndex - startCharIndex,
            sourceRunIndex,
            text.Substring(startCharIndex, endCharIndex - startCharIndex));
    }

    private static SvgTextLayoutResolvedRun CreateResolvedRun(
        int runIndex,
        string text,
        IReadOnlyList<SvgTextLayoutCodepoint> codepoints,
        IReadOnlyList<SvgTextCluster> clusters,
        SvgTextDirection direction,
        int bidiLevel)
    {
        var startClusterIndex = clusters.Min(static cluster => cluster.ClusterIndex);
        var endClusterIndex = clusters.Max(static cluster => cluster.ClusterIndex);
        var startCharIndex = clusters.Min(static cluster => cluster.StartCharIndex);
        var endCharIndex = clusters.Max(static cluster => cluster.StartCharIndex + cluster.Length);
        var sourceRunIndexes = clusters
            .SelectMany(cluster => Enumerable.Range(cluster.StartCodepointIndex, cluster.CodepointCount))
            .Select(index => codepoints[index].SourceRunIndex)
            .Distinct()
            .OrderBy(static index => index)
            .ToArray();

        return new SvgTextLayoutResolvedRun(
            runIndex,
            startClusterIndex,
            endClusterIndex - startClusterIndex + 1,
            startCharIndex,
            endCharIndex - startCharIndex,
            direction,
            bidiLevel,
            sourceRunIndexes,
            text.Substring(startCharIndex, endCharIndex - startCharIndex));
    }

    private static void ApplyClusterIndexes(
        IReadOnlyList<MutableCodepoint> codepoints,
        IReadOnlyList<SvgTextCluster> clusters)
    {
        for (var clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
        {
            var cluster = clusters[clusterIndex];
            for (var i = 0; i < cluster.CodepointCount; i++)
            {
                codepoints[cluster.StartCodepointIndex + i].ClusterIndex = clusterIndex;
            }
        }
    }

    private static SvgTextLayoutResolvedRun[] CreateResolvedRuns(
        string text,
        IReadOnlyList<MutableCodepoint> codepoints,
        IReadOnlyList<SvgTextCluster> clusters,
        SvgTextLayoutStyle paragraphStyle,
        bool visualOrder)
    {
        if (clusters.Count == 0)
        {
            return Array.Empty<SvgTextLayoutResolvedRun>();
        }

        var bidiSpans = CreateBidiSpans(codepoints, paragraphStyle);
        var bidiRuns = bidiSpans.Length == 0
            ? SvgTextBidiResolver.CreateVisualRuns(text, paragraphStyle.Direction, paragraphStyle.UnicodeBidi)
            : SvgTextBidiResolver.CreateVisualRuns(text, paragraphStyle.Direction, paragraphStyle.UnicodeBidi, bidiSpans);
        var orderedBidiRuns = visualOrder
            ? bidiRuns
            : bidiRuns.OrderBy(static run => run.StartCharIndex).ToList();
        var resolvedRuns = new List<SvgTextLayoutResolvedRun>(orderedBidiRuns.Count);
        var assignedVisualClusters = visualOrder ? new HashSet<int>() : null;

        for (var i = 0; i < orderedBidiRuns.Count; i++)
        {
            var bidiRun = orderedBidiRuns[i];
            var runClusters = GetRunClusters(bidiRun, clusters, assignedVisualClusters);
            if (runClusters.Count == 0)
            {
                continue;
            }

            resolvedRuns.Add(CreateResolvedRun(
                resolvedRuns.Count,
                text,
                codepoints,
                runClusters,
                bidiRun.Direction,
                bidiRun.Level));
        }

        return MergeAdjacentCompatibleRuns(resolvedRuns);
    }

    private static SvgTextBidiSpan[] CreateBidiSpans(
        IReadOnlyList<MutableCodepoint> codepoints,
        SvgTextLayoutStyle paragraphStyle)
    {
        if (codepoints.Count == 0)
        {
            return Array.Empty<SvgTextBidiSpan>();
        }

        var spans = new List<SvgTextBidiSpan>();
        var startCodepointIndex = 0;
        for (var i = 1; i <= codepoints.Count; i++)
        {
            if (i < codepoints.Count && codepoints[i].Style.Equals(codepoints[startCodepointIndex].Style))
            {
                continue;
            }

            var style = codepoints[startCodepointIndex].Style;
            if (style.UnicodeBidi != SvgUnicodeBidiMode.Normal &&
                (style.UnicodeBidi != paragraphStyle.UnicodeBidi || style.Direction != paragraphStyle.Direction))
            {
                var first = codepoints[startCodepointIndex];
                var last = codepoints[i - 1];
                spans.Add(new SvgTextBidiSpan(
                    first.StartCharIndex,
                    last.StartCharIndex + last.CharLength - first.StartCharIndex,
                    style.Direction,
                    style.UnicodeBidi));
            }

            startCodepointIndex = i;
        }

        return spans.ToArray();
    }

    private static List<SvgTextCluster> GetRunClusters(
        SvgTextBidiRun bidiRun,
        IReadOnlyList<SvgTextCluster> clusters,
        HashSet<int>? assignedVisualClusters)
    {
        var runClusters = new List<SvgTextCluster>();
        var runEnd = bidiRun.StartCharIndex + bidiRun.Length;
        for (var clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
        {
            var cluster = clusters[clusterIndex];
            var clusterEnd = cluster.StartCharIndex + cluster.Length;
            if (cluster.StartCharIndex >= runEnd || clusterEnd <= bidiRun.StartCharIndex)
            {
                continue;
            }

            if (assignedVisualClusters is not null && !assignedVisualClusters.Add(cluster.ClusterIndex))
            {
                continue;
            }

            runClusters.Add(cluster);
        }

        return runClusters;
    }

    private static SvgTextLayoutResolvedRun CreateResolvedRun(
        int runIndex,
        string text,
        IReadOnlyList<MutableCodepoint> codepoints,
        IReadOnlyList<SvgTextCluster> clusters,
        SvgTextDirection direction,
        int bidiLevel)
    {
        var startClusterIndex = clusters.Min(static cluster => cluster.ClusterIndex);
        var endClusterIndex = clusters.Max(static cluster => cluster.ClusterIndex);
        var startCharIndex = clusters.Min(static cluster => cluster.StartCharIndex);
        var endCharIndex = clusters.Max(static cluster => cluster.StartCharIndex + cluster.Length);
        var sourceRunIndexes = clusters
            .SelectMany(cluster => Enumerable.Range(cluster.StartCodepointIndex, cluster.CodepointCount))
            .Select(index => codepoints[index].SourceRunIndex)
            .Distinct()
            .OrderBy(static index => index)
            .ToArray();

        return new SvgTextLayoutResolvedRun(
            runIndex,
            startClusterIndex,
            endClusterIndex - startClusterIndex + 1,
            startCharIndex,
            endCharIndex - startCharIndex,
            direction,
            bidiLevel,
            sourceRunIndexes,
            text.Substring(startCharIndex, endCharIndex - startCharIndex));
    }

    private static SvgTextLayoutResolvedRun[] MergeAdjacentCompatibleRuns(IReadOnlyList<SvgTextLayoutResolvedRun> runs)
    {
        if (runs.Count <= 1)
        {
            return runs.ToArray();
        }

        var merged = new List<SvgTextLayoutResolvedRun>(runs.Count);
        for (var i = 0; i < runs.Count; i++)
        {
            var current = runs[i];
            if (merged.Count == 0)
            {
                merged.Add(current);
                continue;
            }

            var previous = merged[merged.Count - 1];
            if (previous.Direction == current.Direction &&
                previous.BidiLevel == current.BidiLevel &&
                previous.StartCharIndex + previous.Length == current.StartCharIndex)
            {
                var startCharIndex = previous.StartCharIndex;
                var length = current.StartCharIndex + current.Length - startCharIndex;
                merged[merged.Count - 1] = new SvgTextLayoutResolvedRun(
                    previous.RunIndex,
                    previous.StartClusterIndex,
                    current.StartClusterIndex + current.ClusterCount - previous.StartClusterIndex,
                    startCharIndex,
                    length,
                    previous.Direction,
                    previous.BidiLevel,
                    previous.SourceRunIndexes.Concat(current.SourceRunIndexes).Distinct().OrderBy(static index => index).ToArray(),
                    previous.Text + current.Text);
                continue;
            }

            merged.Add(current with { RunIndex = merged.Count });
        }

        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].RunIndex != i)
            {
                merged[i] = merged[i] with { RunIndex = i };
            }
        }

        return merged.ToArray();
    }

    private static SvgTextBreakOpportunity[] CreateBreakOpportunities(IReadOnlyList<MutableCodepoint> codepoints)
    {
        var opportunities = new List<SvgTextBreakOpportunity>();
        var codepointTexts = codepoints.Select(static codepoint => codepoint.Text).ToArray();
        var bidiFormattingDepth = 0;
        string? previousCodepoint = null;

        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i];
            var nextCodepoint = i + 1 < codepoints.Count ? codepoints[i + 1].Text : null;
            var allowsSoftWrapping = codepoint.Style.AllowsSoftWrapping;
            var isClusterBoundaryAfter = i + 1 >= codepoints.Count ||
                                         codepoint.ClusterIndex != codepoints[i + 1].ClusterIndex;
            var opportunity = SvgTextLineBreakPlanner.GetBreakOpportunity(
                codepointTexts,
                i,
                codepoint.Style.LineBreakOptions,
                bidiFormattingDepth > 0,
                previousCodepoint,
                nextCodepoint,
                isClusterBoundaryAfter);

            if (opportunity.HasValue &&
                (opportunity.Value.Kind == SvgTextBreakOpportunityKind.ForcedLine || allowsSoftWrapping))
            {
                opportunities.Add(opportunity.Value);
            }

            bidiFormattingDepth = SvgTextLineBreakPlanner.UpdateBidiFormattingDepth(bidiFormattingDepth, codepoint.Text);
            previousCodepoint = codepoint.Text == "\n" ? null : codepoint.Text;
        }

        return opportunities.ToArray();
    }

    private static bool HasVisualReordering(
        IReadOnlyList<SvgTextLayoutResolvedRun> logicalRuns,
        IReadOnlyList<SvgTextLayoutResolvedRun> visualRuns)
    {
        if (logicalRuns.Count != visualRuns.Count)
        {
            return true;
        }

        for (var i = 0; i < logicalRuns.Count; i++)
        {
            if (logicalRuns[i].StartCharIndex != visualRuns[i].StartCharIndex ||
                logicalRuns[i].Length != visualRuns[i].Length)
            {
                return true;
            }
        }

        return false;
    }

}
