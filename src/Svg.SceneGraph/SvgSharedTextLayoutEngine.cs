using System;
using System.Collections.Generic;
using System.Text;
using Svg;

namespace Svg.Skia;

internal readonly record struct SvgSharedTextLayoutRun(
    SvgTextBase StyleSource,
    string Text,
    float Advance,
    int SourceCodepointIndex = -1);

internal readonly record struct SvgSharedTextLayoutSegment(
    SvgSharedTextLayoutRun[] Runs,
    float Advance,
    bool IsWhitespace,
    bool ForcesLineBreak);

internal readonly record struct SvgSharedTextLineAreaFragment(float Start, float End)
{
    public float InlineSize => Math.Max(0f, End - Start);
}

internal readonly record struct SvgSharedTextLineArea(
    float Start,
    float InlineSize,
    float BlockCoordinate,
    SvgSharedTextLineAreaFragment[]? Fragments = null,
    int InlineProgression = 1);

internal readonly record struct SvgSharedTextLogicalLine(
    SvgSharedTextLayoutRun[] Runs,
    float Advance,
    SvgSharedTextLineArea Area,
    int LineIndex);

internal static class SvgSharedTextLayoutEngine
{
    public static SvgSharedTextLayoutRun[] CreateLineScopedVisualRuns(
        SvgTextBase paragraph,
        IReadOnlyList<SvgSharedTextLayoutRun> logicalRuns)
    {
        if (logicalRuns.Count == 0)
        {
            return ToArray(logicalRuns);
        }

        var text = ConcatenateRunText(logicalRuns);
        if (string.IsNullOrEmpty(text))
        {
            return ToArray(logicalRuns);
        }

        var paragraphDirection = SvgTextBidiResolver.ResolveDirection(paragraph);
        var paragraphMode = SvgTextBidiResolver.ResolveUnicodeBidi(paragraph);
        var spans = CreateBidiSpans(paragraph, logicalRuns, paragraphDirection, paragraphMode, out var hasNestedBidiControls);
        if (!hasNestedBidiControls && !SvgTextBidiResolver.NeedsVisualOrdering(paragraph, text))
        {
            return ToArray(logicalRuns);
        }

        var bidiRuns = spans.Length == 0
            ? SvgTextBidiResolver.CreateVisualRuns(text, paragraphDirection, paragraphMode)
            : SvgTextBidiResolver.CreateVisualRuns(text, paragraphDirection, paragraphMode, spans);
        if (bidiRuns.Count <= 1 ||
            !TryMapVisualBidiRuns(logicalRuns, bidiRuns, out var visualRuns) ||
            HasSameRunOrder(logicalRuns, visualRuns))
        {
            return ToArray(logicalRuns);
        }

        return visualRuns;
    }

    public static List<SvgSharedTextLogicalLine> CreateWrappedLogicalLines(
        IReadOnlyList<SvgSharedTextLayoutSegment> segments,
        Func<int, SvgSharedTextLineArea> resolveLineArea,
        int maxLineSearchCount,
        bool preserveLineEdgeWhitespace,
        float textLengthTolerance,
        Func<SvgSharedTextLayoutRun, bool> isWhitespaceRun)
    {
        return CreateWrappedLogicalLines(
            segments,
            resolveLineArea,
            new SvgTextWrappingOptions(maxLineSearchCount, preserveLineEdgeWhitespace, textLengthTolerance),
            isWhitespaceRun);
    }

    public static List<SvgSharedTextLogicalLine> CreateWrappedLogicalLines(
        IReadOnlyList<SvgSharedTextLayoutSegment> segments,
        Func<int, SvgSharedTextLineArea> resolveLineArea,
        SvgTextWrappingOptions options,
        Func<SvgSharedTextLayoutRun, bool> isWhitespaceRun)
    {
        var lines = new List<SvgSharedTextLogicalLine>();
        var lineRuns = new List<SvgSharedTextLayoutRun>();
        var lineAdvance = 0f;
        var lineIndex = 0;
        var maxLineSearchCount = options.EffectiveMaxLineSearchCount;
        var preserveLineEdgeWhitespace = options.PreserveLineEdgeWhitespace;
        var textLengthTolerance = options.EffectiveTextLengthTolerance;
        var physicalLineArea = resolveLineArea(lineIndex);
        var lineAreaFragments = ResolveFragments(physicalLineArea);
        var lineAreaFragmentIndex = 0;
        var lineArea = GetFragmentArea(physicalLineArea, lineAreaFragments, lineAreaFragmentIndex);

        void TrimTrailingWhitespace()
        {
            if (preserveLineEdgeWhitespace)
            {
                return;
            }

            while (lineRuns.Count > 0 && isWhitespaceRun(lineRuns[lineRuns.Count - 1]))
            {
                lineAdvance -= lineRuns[lineRuns.Count - 1].Advance;
                lineRuns.RemoveAt(lineRuns.Count - 1);
            }
        }

        void AdvanceLineArea()
        {
            lineIndex++;
            physicalLineArea = lineIndex < maxLineSearchCount
                ? resolveLineArea(lineIndex)
                : new SvgSharedTextLineArea(lineArea.Start, 0f, lineArea.BlockCoordinate, lineArea.Fragments, lineArea.InlineProgression);
            lineAreaFragments = ResolveFragments(physicalLineArea);
            lineAreaFragmentIndex = 0;
            lineArea = GetFragmentArea(physicalLineArea, lineAreaFragments, lineAreaFragmentIndex);
        }

        void AdvanceFragmentOrLine()
        {
            if (lineAreaFragmentIndex + 1 < lineAreaFragments.Length)
            {
                lineAreaFragmentIndex++;
                lineArea = GetFragmentArea(physicalLineArea, lineAreaFragments, lineAreaFragmentIndex);
                return;
            }

            AdvanceLineArea();
        }

        void FlushLine(bool advanceEmptyLine)
        {
            TrimTrailingWhitespace();
            if (lineRuns.Count == 0)
            {
                lineAdvance = 0f;
                if (advanceEmptyLine)
                {
                    AdvanceFragmentOrLine();
                }

                return;
            }

            lines.Add(new SvgSharedTextLogicalLine(lineRuns.ToArray(), Math.Max(0f, lineAdvance), lineArea, lineIndex));
            lineRuns.Clear();
            lineAdvance = 0f;
            AdvanceFragmentOrLine();
        }

        void AppendSegment(SvgSharedTextLayoutSegment segment)
        {
            for (var i = 0; i < segment.Runs.Length; i++)
            {
                lineRuns.Add(segment.Runs[i]);
            }

            lineAdvance += segment.Advance;
        }

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.ForcesLineBreak)
            {
                FlushLine(advanceEmptyLine: true);
                continue;
            }

            while (lineRuns.Count == 0 &&
                   lineArea.InlineSize <= 0f &&
                   HasMoreAreas(lineIndex, maxLineSearchCount, lineAreaFragmentIndex, lineAreaFragments.Length))
            {
                AdvanceFragmentOrLine();
            }

            if (lineArea.InlineSize <= 0f)
            {
                continue;
            }

            if (segment.IsWhitespace && lineRuns.Count == 0 && !preserveLineEdgeWhitespace)
            {
                continue;
            }

            var candidateAdvance = lineAdvance + segment.Advance;
            if (!segment.IsWhitespace &&
                lineRuns.Count > 0 &&
                candidateAdvance > lineArea.InlineSize + textLengthTolerance)
            {
                FlushLine(advanceEmptyLine: false);
                while (lineRuns.Count == 0 &&
                       lineArea.InlineSize <= 0f &&
                       HasMoreAreas(lineIndex, maxLineSearchCount, lineAreaFragmentIndex, lineAreaFragments.Length))
                {
                    AdvanceFragmentOrLine();
                }

                if (lineArea.InlineSize <= 0f)
                {
                    continue;
                }
            }
            else if (segment.IsWhitespace &&
                     candidateAdvance > lineArea.InlineSize + textLengthTolerance)
            {
                if (!preserveLineEdgeWhitespace || lineRuns.Count == 0)
                {
                    continue;
                }

                FlushLine(advanceEmptyLine: false);
                while (lineRuns.Count == 0 &&
                       lineArea.InlineSize <= 0f &&
                       HasMoreAreas(lineIndex, maxLineSearchCount, lineAreaFragmentIndex, lineAreaFragments.Length))
                {
                    AdvanceFragmentOrLine();
                }

                if (lineArea.InlineSize <= 0f)
                {
                    continue;
                }
            }

            if (segment.IsWhitespace && lineRuns.Count == 0 && !preserveLineEdgeWhitespace)
            {
                continue;
            }

            AppendSegment(segment);

            if (!segment.IsWhitespace &&
                lineAdvance > lineArea.InlineSize + textLengthTolerance)
            {
                FlushLine(advanceEmptyLine: false);
            }
        }

        FlushLine(advanceEmptyLine: false);
        return lines;
    }

    private static string ConcatenateRunText(IReadOnlyList<SvgSharedTextLayoutRun> runs)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < runs.Count; i++)
        {
            builder.Append(runs[i].Text);
        }

        return builder.ToString();
    }

    private static SvgTextBidiSpan[] CreateBidiSpans(
        SvgTextBase paragraph,
        IReadOnlyList<SvgSharedTextLayoutRun> runs,
        SvgTextDirection paragraphDirection,
        SvgUnicodeBidiMode paragraphMode,
        out bool hasNestedBidiControls)
    {
        hasNestedBidiControls = false;
        var spans = new List<SvgTextBidiSpan>();
        var charIndex = 0;
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            var length = run.Text.Length;
            if (length > 0)
            {
                var mode = SvgTextBidiResolver.ResolveUnicodeBidi(run.StyleSource);
                var direction = SvgTextBidiResolver.ResolveDirection(run.StyleSource);
                if (mode != SvgUnicodeBidiMode.Normal &&
                    (!ReferenceEquals(run.StyleSource, paragraph) ||
                     mode != paragraphMode ||
                     direction != paragraphDirection))
                {
                    spans.Add(new SvgTextBidiSpan(charIndex, length, direction, mode));
                    hasNestedBidiControls = true;
                }
            }

            charIndex += length;
        }

        return spans.ToArray();
    }

    private static bool TryMapVisualBidiRuns(
        IReadOnlyList<SvgSharedTextLayoutRun> logicalRuns,
        IReadOnlyList<SvgTextBidiRun> bidiRuns,
        out SvgSharedTextLayoutRun[] visualRuns)
    {
        visualRuns = Array.Empty<SvgSharedTextLayoutRun>();
        var runEndCharIndexes = new int[logicalRuns.Count];
        var charIndex = 0;
        for (var i = 0; i < logicalRuns.Count; i++)
        {
            charIndex += logicalRuns[i].Text.Length;
            runEndCharIndexes[i] = charIndex;
        }

        var orderedRuns = new List<SvgSharedTextLayoutRun>(bidiRuns.Count);
        var mappedCharLength = 0;
        for (var bidiRunIndex = 0; bidiRunIndex < bidiRuns.Count; bidiRunIndex++)
        {
            var bidiRun = bidiRuns[bidiRunIndex];
            var startCharIndex = bidiRun.StartCharIndex;
            var endCharIndex = startCharIndex + bidiRun.Length;
            while (startCharIndex < endCharIndex)
            {
                var runIndex = GetRunIndex(runEndCharIndexes, startCharIndex);
                if (runIndex < 0)
                {
                    return false;
                }

                var previousRunEnd = runIndex == 0 ? 0 : runEndCharIndexes[runIndex - 1];
                var chunkEndCharIndex = Math.Min(endCharIndex, runEndCharIndexes[runIndex]);
                var chunkStartInRun = startCharIndex - previousRunEnd;
                var chunkLength = chunkEndCharIndex - startCharIndex;
                if (chunkLength <= 0 ||
                    !TryCreateRunSlice(logicalRuns[runIndex], chunkStartInRun, chunkLength, out var slice))
                {
                    return false;
                }

                orderedRuns.Add(slice);
                mappedCharLength += chunkLength;
                startCharIndex = chunkEndCharIndex;
            }
        }

        if (mappedCharLength != charIndex)
        {
            return false;
        }

        visualRuns = orderedRuns.ToArray();
        return true;
    }

    private static bool TryCreateRunSlice(
        SvgSharedTextLayoutRun source,
        int startCharIndex,
        int length,
        out SvgSharedTextLayoutRun slice)
    {
        slice = default;
        if (startCharIndex < 0 ||
            length <= 0 ||
            startCharIndex + length > source.Text.Length)
        {
            return false;
        }

        if (startCharIndex == 0 && length == source.Text.Length)
        {
            slice = source;
            return true;
        }

        var text = source.Text.Substring(startCharIndex, length);
        var advance = source.Text.Length > 0
            ? source.Advance * length / source.Text.Length
            : 0f;
        slice = new SvgSharedTextLayoutRun(source.StyleSource, text, Math.Max(0f, advance));
        return true;
    }

    private static int GetRunIndex(IReadOnlyList<int> runEndCharIndexes, int charIndex)
    {
        var low = 0;
        var high = runEndCharIndexes.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            if (charIndex < runEndCharIndexes[mid])
            {
                if (mid == 0 || charIndex >= runEndCharIndexes[mid - 1])
                {
                    return mid;
                }

                high = mid - 1;
                continue;
            }

            low = mid + 1;
        }

        return -1;
    }

    private static bool HasSameRunOrder(
        IReadOnlyList<SvgSharedTextLayoutRun> logicalRuns,
        IReadOnlyList<SvgSharedTextLayoutRun> visualRuns)
    {
        if (logicalRuns.Count != visualRuns.Count)
        {
            return false;
        }

        for (var i = 0; i < logicalRuns.Count; i++)
        {
            if (!ReferenceEquals(logicalRuns[i].StyleSource, visualRuns[i].StyleSource) ||
                !string.Equals(logicalRuns[i].Text, visualRuns[i].Text, StringComparison.Ordinal) ||
                logicalRuns[i].SourceCodepointIndex != visualRuns[i].SourceCodepointIndex)
            {
                return false;
            }
        }

        return true;
    }

    private static SvgSharedTextLayoutRun[] ToArray(IReadOnlyList<SvgSharedTextLayoutRun> runs)
    {
        var result = new SvgSharedTextLayoutRun[runs.Count];
        for (var i = 0; i < runs.Count; i++)
        {
            result[i] = runs[i];
        }

        return result;
    }

    private static SvgSharedTextLineAreaFragment[] ResolveFragments(SvgSharedTextLineArea area)
    {
        if (area.Fragments is { Length: > 0 } fragments)
        {
            return fragments;
        }

        return area.InlineSize > 0f
            ? new[] { new SvgSharedTextLineAreaFragment(area.Start, area.Start + area.InlineSize) }
            : Array.Empty<SvgSharedTextLineAreaFragment>();
    }

    private static SvgSharedTextLineArea GetFragmentArea(
        SvgSharedTextLineArea physicalArea,
        IReadOnlyList<SvgSharedTextLineAreaFragment> fragments,
        int fragmentIndex)
    {
        if (fragmentIndex < 0 || fragmentIndex >= fragments.Count)
        {
            return new SvgSharedTextLineArea(physicalArea.Start, 0f, physicalArea.BlockCoordinate, physicalArea.Fragments, physicalArea.InlineProgression);
        }

        var fragment = fragments[fragmentIndex];
        return new SvgSharedTextLineArea(
            fragment.Start,
            fragment.InlineSize,
            physicalArea.BlockCoordinate,
            physicalArea.Fragments,
            physicalArea.InlineProgression);
    }

    private static bool HasMoreAreas(
        int lineIndex,
        int maxLineSearchCount,
        int fragmentIndex,
        int fragmentCount)
    {
        return fragmentIndex + 1 < fragmentCount || lineIndex + 1 < maxLineSearchCount;
    }
}
