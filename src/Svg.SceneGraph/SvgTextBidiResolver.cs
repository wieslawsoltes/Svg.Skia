#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Svg.Model;

namespace Svg.Skia;

internal enum SvgTextDirection
{
    LeftToRight = 1,
    RightToLeft = -1
}

internal enum SvgUnicodeBidiMode
{
    Normal,
    Embed,
    Isolate,
    BidiOverride,
    IsolateOverride,
    PlainText
}

internal readonly record struct SvgTextBidiRun(
    int StartCharIndex,
    int Length,
    SvgTextDirection Direction,
    int Level);

internal readonly record struct SvgTextBidiSpan(
    int StartCharIndex,
    int Length,
    SvgTextDirection Direction,
    SvgUnicodeBidiMode Mode)
{
    public int EndCharIndex => StartCharIndex + Length;
}

internal static class SvgTextBidiResolver
{
    private const int MaxExplicitDepth = 125;

    private enum BidiClass
    {
        L,
        R,
        AL,
        EN,
        AN,
        ES,
        ET,
        CS,
        NSM,
        BN,
        B,
        S,
        WS,
        ON,
        LRE,
        RLE,
        LRO,
        RLO,
        PDF,
        LRI,
        RLI,
        FSI,
        PDI
    }

    private sealed class BidiCodepoint
    {
        public BidiCodepoint(string text, int scalar, int charIndex, BidiClass bidiClass, int charLength = -1)
        {
            Text = text;
            Scalar = scalar;
            CharIndex = charIndex;
            CharLength = charLength >= 0 ? charLength : text.Length;
            BidiClass = bidiClass;
            ResolvedClass = bidiClass;
        }

        public string Text { get; }

        public int Scalar { get; }

        public int CharIndex { get; }

        public int CharLength { get; }

        public BidiClass BidiClass { get; }

        public BidiClass ResolvedClass { get; set; }

        public int Level { get; set; }

        public int IsolateDepth { get; set; }

        public bool ForcedOverride { get; set; }

        public bool IsBoundaryNeutral => ResolvedClass is BidiClass.B or BidiClass.S or BidiClass.WS or BidiClass.ON or BidiClass.BN;

        public BidiCodepoint Clone() => new(Text, Scalar, CharIndex, BidiClass, CharLength)
        {
            ResolvedClass = ResolvedClass,
            Level = Level,
            IsolateDepth = IsolateDepth,
            ForcedOverride = ForcedOverride
        };
    }

    private readonly record struct EmbeddingState(int Level, BidiClass? OverrideClass, bool Isolate, int IsolateDepth);

    private readonly record struct BracketPair(int OpenIndex, int CloseIndex);

    public static bool IsRightToLeft(SvgTextBase svgTextBase)
    {
        return ResolveDirection(svgTextBase) == SvgTextDirection.RightToLeft;
    }

    public static SvgTextDirection ResolveDirection(SvgTextBase svgTextBase)
    {
        if (HasDeclaredTextProperty(svgTextBase, "direction"))
        {
            var value = ResolveComputedTextProperty(svgTextBase, "direction", "ltr");
            return value.Equals("rtl", StringComparison.OrdinalIgnoreCase)
                ? SvgTextDirection.RightToLeft
                : SvgTextDirection.LeftToRight;
        }

        return HasRightToLeftWritingMode(svgTextBase)
            ? SvgTextDirection.RightToLeft
            : SvgTextDirection.LeftToRight;
    }

    public static SvgUnicodeBidiMode ResolveUnicodeBidi(SvgTextBase svgTextBase)
    {
        if (!HasDeclaredTextProperty(svgTextBase, "unicode-bidi"))
        {
            return SvgUnicodeBidiMode.Normal;
        }

        var value = ResolveComputedTextProperty(svgTextBase, "unicode-bidi", "normal");
        return ParseUnicodeBidiMode(value);
    }

    public static bool NeedsVisualOrdering(SvgTextBase svgTextBase, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var mode = ResolveUnicodeBidi(svgTextBase);
        return mode is SvgUnicodeBidiMode.BidiOverride or SvgUnicodeBidiMode.IsolateOverride or SvgUnicodeBidiMode.PlainText ||
               ContainsMixedStrongDirections(text) ||
               ContainsExplicitBidiControlCodepoint(text);
    }

    public static string ApplyBrowserCompatibleControls(SvgTextBase svgTextBase, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var direction = ResolveDirection(svgTextBase);
        var mode = ResolveUnicodeBidi(svgTextBase);
        if (TryGetVisualText(text, direction, mode, out var visualText))
        {
            return visualText;
        }

        return mode switch
        {
            SvgUnicodeBidiMode.BidiOverride when direction == SvgTextDirection.RightToLeft => "\u202E" + text + "\u202C",
            SvgUnicodeBidiMode.BidiOverride => "\u202D" + text + "\u202C",
            SvgUnicodeBidiMode.IsolateOverride when direction == SvgTextDirection.RightToLeft => "\u2067\u202E" + text + "\u202C\u2069",
            SvgUnicodeBidiMode.IsolateOverride => "\u2066\u202D" + text + "\u202C\u2069",
            SvgUnicodeBidiMode.Embed when direction == SvgTextDirection.RightToLeft => "\u202B" + text + "\u202C",
            SvgUnicodeBidiMode.Embed => "\u202A" + text + "\u202C",
            SvgUnicodeBidiMode.Isolate when direction == SvgTextDirection.RightToLeft => "\u2067" + text + "\u2069",
            SvgUnicodeBidiMode.Isolate => "\u2066" + text + "\u2069",
            _ when direction == SvgTextDirection.RightToLeft => "\u202B" + text + "\u202C",
            _ => text
        };
    }

    public static bool TryGetVisualText(
        string text,
        SvgTextDirection baseDirection,
        SvgUnicodeBidiMode mode,
        out string visualText)
    {
        visualText = text;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (mode is SvgUnicodeBidiMode.BidiOverride or SvgUnicodeBidiMode.IsolateOverride)
        {
            if (baseDirection == SvgTextDirection.RightToLeft)
            {
                visualText = ReverseByCodepoint(text);
                return !visualText.Equals(text, StringComparison.Ordinal);
            }

            return false;
        }

        if (!ContainsMixedStrongDirections(text) && !ContainsExplicitBidiControlCodepoint(text))
        {
            return false;
        }

        var visualRuns = CreateVisualRuns(text, baseDirection, mode);
        if (visualRuns.Count <= 1)
        {
            return false;
        }

        visualText = string.Concat(visualRuns.Select(run => text.Substring(run.StartCharIndex, run.Length)));
        return !visualText.Equals(text, StringComparison.Ordinal);
    }

    public static List<SvgTextBidiRun> CreateVisualRuns(
        string text,
        SvgTextDirection baseDirection,
        SvgUnicodeBidiMode mode)
    {
        var codepoints = CreateCodepoints(text);
        return CreateVisualRuns(codepoints, baseDirection, mode);
    }

    public static List<SvgTextBidiRun> CreateVisualRuns(
        string text,
        SvgTextDirection baseDirection,
        SvgUnicodeBidiMode mode,
        IReadOnlyList<SvgTextBidiSpan> spans)
    {
        if (spans.Count == 0)
        {
            return CreateVisualRuns(text, baseDirection, mode);
        }

        var codepoints = CreateCodepoints(text, baseDirection, mode, spans);
        return CreateVisualRuns(codepoints, baseDirection, mode);
    }

    public static List<SvgTextBidiRun> CreateLineVisualRuns(
        IReadOnlyList<SvgTextBidiRun> paragraphRuns,
        int lineStartCharIndex,
        int lineLength,
        SvgTextDirection baseDirection)
    {
        if (paragraphRuns.Count == 0 || lineLength <= 0)
        {
            return new List<SvgTextBidiRun>();
        }

        var lineEndCharIndex = lineStartCharIndex + lineLength;
        var lineRuns = new List<SvgTextBidiRun>();
        for (var i = 0; i < paragraphRuns.Count; i++)
        {
            var run = paragraphRuns[i];
            var runEndCharIndex = run.StartCharIndex + run.Length;
            var start = Math.Max(lineStartCharIndex, run.StartCharIndex);
            var end = Math.Min(lineEndCharIndex, runEndCharIndex);
            if (end <= start)
            {
                continue;
            }

            lineRuns.Add(new SvgTextBidiRun(start, end - start, run.Direction, run.Level));
        }

        return lineRuns.Count <= 1
            ? lineRuns
            : ReorderRunsVisually(lineRuns, baseDirection);
    }

    private static List<SvgTextBidiRun> CreateVisualRuns(
        List<BidiCodepoint> codepoints,
        SvgTextDirection baseDirection,
        SvgUnicodeBidiMode mode)
    {
        if (codepoints.Count == 0)
        {
            return new List<SvgTextBidiRun>();
        }

        if (mode == SvgUnicodeBidiMode.PlainText && HasParagraphSeparators(codepoints))
        {
            return CreatePlainTextVisualRuns(codepoints, baseDirection);
        }

        var paragraphDirection = mode == SvgUnicodeBidiMode.PlainText
            ? ResolvePlainTextDirection(codepoints, baseDirection)
            : baseDirection;
        return CreateParagraphVisualRuns(codepoints, paragraphDirection, mode);
    }

    public static bool ContainsMixedStrongDirections(string text)
    {
        if (IsAsciiText(text))
        {
            return false;
        }

        var hasLeftToRight = false;
        var hasRightToLeft = false;
        foreach (var codepoint in CreateCodepoints(text))
        {
            var direction = GetStrongDirection(codepoint.BidiClass);
            if (direction == SvgTextDirection.LeftToRight)
            {
                hasLeftToRight = true;
            }
            else if (direction == SvgTextDirection.RightToLeft)
            {
                hasRightToLeft = true;
            }

            if (hasLeftToRight && hasRightToLeft)
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsRightToLeftStrongDirection(string text)
    {
        if (IsAsciiText(text))
        {
            return false;
        }

        foreach (var codepoint in CreateCodepoints(text))
        {
            if (GetStrongDirection(codepoint.BidiClass) == SvgTextDirection.RightToLeft)
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsExplicitBidiControlCodepoint(string text)
    {
        if (IsAsciiText(text))
        {
            return false;
        }

        foreach (var codepoint in CreateCodepoints(text))
        {
            if (IsExplicitBidiClass(codepoint.BidiClass))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAsciiText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsExplicitBidiControlCodepoint(string codepoint)
    {
        return !string.IsNullOrEmpty(codepoint) &&
               IsExplicitBidiClass(GetBidiClass(char.ConvertToUtf32(codepoint, 0)));
    }

    public static int GetStrongDirection(string codepoint)
    {
        if (string.IsNullOrEmpty(codepoint))
        {
            return 0;
        }

        return GetStrongDirection(GetBidiClass(char.ConvertToUtf32(codepoint, 0))) switch
        {
            SvgTextDirection.LeftToRight => 1,
            SvgTextDirection.RightToLeft => -1,
            _ => 0
        };
    }

    private static string ResolveComputedTextProperty(SvgTextBase svgTextBase, string propertyName, string fallback)
    {
        return svgTextBase.ComputedStyle.TryGetPropertyValue(propertyName, out var value) &&
               !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static SvgUnicodeBidiMode ParseUnicodeBidiMode(string value)
    {
        var normalized = value.AsSpan().Trim();
        if (normalized.Equals("embed".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return SvgUnicodeBidiMode.Embed;
        }

        if (normalized.Equals("isolate".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return SvgUnicodeBidiMode.Isolate;
        }

        if (normalized.Equals("bidi-override".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return SvgUnicodeBidiMode.BidiOverride;
        }

        if (normalized.Equals("isolate-override".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return SvgUnicodeBidiMode.IsolateOverride;
        }

        return normalized.Equals("plaintext".AsSpan(), StringComparison.OrdinalIgnoreCase)
            ? SvgUnicodeBidiMode.PlainText
            : SvgUnicodeBidiMode.Normal;
    }

    private static bool HasDeclaredTextProperty(SvgTextBase svgTextBase, string propertyName)
    {
        for (SvgElement? current = svgTextBase; current is not null; current = current.Parent)
        {
            if (current.TryGetOwnCascadedStyleValue(propertyName, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRightToLeftWritingMode(SvgTextBase svgTextBase)
    {
        for (SvgElement? current = svgTextBase; current is not null; current = current.Parent)
        {
            if (current is SvgTextSpan &&
                current.TryGetAttribute("writing-mode", out _))
            {
                continue;
            }

            if (current.TryGetOwnCascadedStyleValue("writing-mode", out var writingMode) &&
                !string.IsNullOrWhiteSpace(writingMode))
            {
                var normalized = writingMode.AsSpan().Trim();
                return normalized.Equals("rl".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                       normalized.Equals("rl-tb".AsSpan(), StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static List<BidiCodepoint> CreateCodepoints(string text)
    {
        var codepoints = new List<BidiCodepoint>();
        var charIndex = 0;
        while (charIndex < text.Length)
        {
            var start = charIndex;
            var scalar = char.ConvertToUtf32(text, charIndex);
            charIndex += scalar > 0xFFFF ? 2 : 1;
            codepoints.Add(new BidiCodepoint(char.ConvertFromUtf32(scalar), scalar, start, GetBidiClass(scalar)));
        }

        return codepoints;
    }

    private static List<BidiCodepoint> CreateCodepoints(
        string text,
        SvgTextDirection baseDirection,
        SvgUnicodeBidiMode paragraphMode,
        IReadOnlyList<SvgTextBidiSpan> spans)
    {
        var codepoints = new List<BidiCodepoint>();
        var orderedSpans = spans
            .Where(static span => span.Length > 0)
            .OrderBy(static span => span.StartCharIndex)
            .ToArray();
        var charIndex = 0;
        for (var i = 0; i < orderedSpans.Length; i++)
        {
            var span = orderedSpans[i];
            var spanStart = Clamp(span.StartCharIndex, 0, text.Length);
            var spanEnd = Clamp(span.EndCharIndex, spanStart, text.Length);
            if (spanEnd <= charIndex)
            {
                continue;
            }

            if (spanStart > charIndex)
            {
                AddSourceCodepoints(text, charIndex, spanStart, codepoints);
            }

            var wrapped = TryGetSpanBoundaryControls(span, baseDirection, paragraphMode, out var openControls, out var closeControls);
            if (wrapped)
            {
                AddSyntheticControls(openControls, spanStart, codepoints);
            }

            AddSourceCodepoints(text, spanStart, spanEnd, codepoints);

            if (wrapped)
            {
                AddSyntheticControls(closeControls, spanEnd, codepoints);
            }

            charIndex = spanEnd;
        }

        if (charIndex < text.Length)
        {
            AddSourceCodepoints(text, charIndex, text.Length, codepoints);
        }

        return codepoints;
    }

    private static void AddSourceCodepoints(string text, int startCharIndex, int endCharIndex, List<BidiCodepoint> codepoints)
    {
        var charIndex = startCharIndex;
        while (charIndex < endCharIndex)
        {
            var scalar = char.ConvertToUtf32(text, charIndex);
            var codepointText = char.ConvertFromUtf32(scalar);
            codepoints.Add(new BidiCodepoint(codepointText, scalar, charIndex, GetBidiClass(scalar)));
            charIndex += scalar > 0xFFFF ? 2 : 1;
        }
    }

    private static void AddSyntheticControls(string controls, int sourceCharIndex, List<BidiCodepoint> codepoints)
    {
        for (var i = 0; i < controls.Length;)
        {
            var scalar = char.ConvertToUtf32(controls, i);
            var codepointText = char.ConvertFromUtf32(scalar);
            codepoints.Add(new BidiCodepoint(codepointText, scalar, sourceCharIndex, GetBidiClass(scalar), charLength: 0));
            i += scalar > 0xFFFF ? 2 : 1;
        }
    }

    private static bool TryGetSpanBoundaryControls(
        SvgTextBidiSpan span,
        SvgTextDirection baseDirection,
        SvgUnicodeBidiMode paragraphMode,
        out string openControls,
        out string closeControls)
    {
        openControls = string.Empty;
        closeControls = string.Empty;
        if (span.Mode == SvgUnicodeBidiMode.Normal)
        {
            return false;
        }

        var leftToRight = span.Direction == SvgTextDirection.LeftToRight;
        switch (span.Mode)
        {
            case SvgUnicodeBidiMode.Embed:
                openControls = leftToRight ? "\u202A" : "\u202B";
                closeControls = "\u202C";
                return true;

            case SvgUnicodeBidiMode.BidiOverride:
                openControls = leftToRight ? "\u202D" : "\u202E";
                closeControls = "\u202C";
                return true;

            case SvgUnicodeBidiMode.Isolate:
                openControls = leftToRight ? "\u2066" : "\u2067";
                closeControls = "\u2069";
                return true;

            case SvgUnicodeBidiMode.IsolateOverride:
                openControls = leftToRight ? "\u2066\u202D" : "\u2067\u202E";
                closeControls = "\u202C\u2069";
                return true;

            case SvgUnicodeBidiMode.PlainText:
                openControls = "\u2068";
                closeControls = "\u2069";
                return true;

            default:
                return false;
        }
    }

    private static bool HasParagraphSeparators(IReadOnlyList<BidiCodepoint> codepoints)
    {
        for (var i = 0; i < codepoints.Count; i++)
        {
            if (codepoints[i].BidiClass == BidiClass.B)
            {
                return true;
            }
        }

        return false;
    }

    private static List<SvgTextBidiRun> CreatePlainTextVisualRuns(IReadOnlyList<BidiCodepoint> codepoints, SvgTextDirection fallbackDirection)
    {
        var visualRuns = new List<SvgTextBidiRun>();
        var paragraphStart = 0;
        for (var i = 0; i <= codepoints.Count; i++)
        {
            if (i < codepoints.Count && codepoints[i].BidiClass != BidiClass.B)
            {
                continue;
            }

            if (i > paragraphStart)
            {
                var paragraphCodepoints = CloneRange(codepoints, paragraphStart, i);
                var paragraphDirection = ResolvePlainTextDirection(paragraphCodepoints, fallbackDirection);
                visualRuns.AddRange(CreateParagraphVisualRuns(paragraphCodepoints, paragraphDirection, SvgUnicodeBidiMode.Normal));
            }

            if (i < codepoints.Count)
            {
                var separator = codepoints[i];
                if (separator.CharLength > 0)
                {
                    visualRuns.Add(new SvgTextBidiRun(
                        separator.CharIndex,
                        separator.CharLength,
                        fallbackDirection,
                        fallbackDirection == SvgTextDirection.RightToLeft ? 1 : 0));
                }
            }

            paragraphStart = i + 1;
        }

        return visualRuns;
    }

    private static List<BidiCodepoint> CloneRange(IReadOnlyList<BidiCodepoint> codepoints, int startIndex, int endIndex)
    {
        var range = new List<BidiCodepoint>(endIndex - startIndex);
        for (var i = startIndex; i < endIndex; i++)
        {
            range.Add(codepoints[i].Clone());
        }

        return range;
    }

    private static List<SvgTextBidiRun> CreateParagraphVisualRuns(
        List<BidiCodepoint> codepoints,
        SvgTextDirection paragraphDirection,
        SvgUnicodeBidiMode mode)
    {
        ResolveExplicitLevels(codepoints, paragraphDirection, mode);
        ResolveWeakClasses(codepoints, paragraphDirection);
        ResolveNeutralClasses(codepoints, paragraphDirection);
        ResolveImplicitLevels(codepoints);

        var logicalRuns = CreateLogicalRuns(codepoints, mode);
        if (logicalRuns.Count <= 1)
        {
            return logicalRuns;
        }

        return ReorderRunsVisually(logicalRuns, paragraphDirection);
    }

    private static SvgTextDirection ResolvePlainTextDirection(IReadOnlyList<BidiCodepoint> codepoints, SvgTextDirection fallback)
    {
        for (var i = 0; i < codepoints.Count; i++)
        {
            var strongDirection = GetStrongDirection(codepoints[i].BidiClass);
            if (strongDirection.HasValue)
            {
                return strongDirection.Value;
            }
        }

        return fallback;
    }

    private static void ResolveExplicitLevels(IReadOnlyList<BidiCodepoint> codepoints, SvgTextDirection baseDirection, SvgUnicodeBidiMode mode)
    {
        var baseLevel = baseDirection == SvgTextDirection.RightToLeft ? 1 : 0;
        var current = new EmbeddingState(
            baseLevel,
            mode is SvgUnicodeBidiMode.BidiOverride or SvgUnicodeBidiMode.IsolateOverride
                ? ToStrongClass(baseDirection)
                : null,
            mode is SvgUnicodeBidiMode.Isolate or SvgUnicodeBidiMode.IsolateOverride,
            mode is SvgUnicodeBidiMode.Isolate or SvgUnicodeBidiMode.IsolateOverride ? 1 : 0);
        var stack = new Stack<EmbeddingState>();

        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i];
            codepoint.Level = current.Level;
            codepoint.IsolateDepth = current.IsolateDepth;
            codepoint.ResolvedClass = codepoint.BidiClass;

            switch (codepoint.BidiClass)
            {
                case BidiClass.LRE:
                case BidiClass.RLE:
                case BidiClass.LRO:
                case BidiClass.RLO:
                case BidiClass.LRI:
                case BidiClass.RLI:
                case BidiClass.FSI:
                    codepoint.ResolvedClass = BidiClass.BN;
                    if (stack.Count >= MaxExplicitDepth)
                    {
                        break;
                    }

                    var nextDirection = codepoint.BidiClass switch
                    {
                        BidiClass.LRE or BidiClass.LRO or BidiClass.LRI => SvgTextDirection.LeftToRight,
                        BidiClass.RLE or BidiClass.RLO or BidiClass.RLI => SvgTextDirection.RightToLeft,
                        BidiClass.FSI => ResolveFirstStrongIsolateDirection(codepoints, i + 1, baseDirection),
                        _ => baseDirection
                    };
                    var nextLevel = GetNextEmbeddingLevel(current.Level, nextDirection);
                    if (nextLevel <= MaxExplicitDepth)
                    {
                        var isIsolateInitiator = codepoint.BidiClass is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;
                        stack.Push(current);
                        current = new EmbeddingState(
                            nextLevel,
                            codepoint.BidiClass is BidiClass.LRO
                                ? BidiClass.L
                                : codepoint.BidiClass is BidiClass.RLO
                                    ? BidiClass.R
                                    : null,
                            isIsolateInitiator,
                            current.IsolateDepth + (isIsolateInitiator ? 1 : 0));
                    }

                    break;

                case BidiClass.PDF:
                    codepoint.ResolvedClass = BidiClass.BN;
                    if (stack.Count > 0 && !current.Isolate)
                    {
                        current = stack.Pop();
                    }

                    break;

                case BidiClass.PDI:
                    codepoint.ResolvedClass = BidiClass.BN;
                    while (stack.Count > 0)
                    {
                        var previous = stack.Pop();
                        if (current.Isolate)
                        {
                            current = previous;
                            break;
                        }

                        current = previous;
                    }

                    break;

                default:
                    if (current.OverrideClass.HasValue)
                    {
                        codepoint.ResolvedClass = current.OverrideClass.Value;
                        codepoint.ForcedOverride = true;
                    }

                    break;
            }
        }
    }

    private static void ResolveWeakClasses(IReadOnlyList<BidiCodepoint> codepoints, SvgTextDirection baseDirection)
    {
        var previousClassByIsolateDepth = new Dictionary<int, BidiClass>();
        for (var i = 0; i < codepoints.Count; i++)
        {
            var bidiClass = codepoints[i].ResolvedClass;
            if (bidiClass == BidiClass.NSM)
            {
                codepoints[i].ResolvedClass = previousClassByIsolateDepth.TryGetValue(codepoints[i].IsolateDepth, out var previousClass)
                    ? previousClass
                    : StrongClassFromLevel(codepoints[i].Level);
                bidiClass = codepoints[i].ResolvedClass;
            }

            if (bidiClass != BidiClass.BN)
            {
                previousClassByIsolateDepth[codepoints[i].IsolateDepth] = bidiClass;
            }
        }

        for (var i = 0; i < codepoints.Count; i++)
        {
            if (codepoints[i].ResolvedClass == BidiClass.EN &&
                FindPreviousStrongClass(codepoints, i - 1, codepoints[i].IsolateDepth) == BidiClass.AL)
            {
                codepoints[i].ResolvedClass = BidiClass.AN;
            }
        }

        for (var i = 0; i < codepoints.Count; i++)
        {
            if (codepoints[i].ResolvedClass == BidiClass.AL)
            {
                codepoints[i].ResolvedClass = BidiClass.R;
            }
        }

        for (var i = 1; i < codepoints.Count - 1; i++)
        {
            var bidiClass = codepoints[i].ResolvedClass;
            var previous = FindPreviousNonRemovedClass(codepoints, i - 1, codepoints[i].IsolateDepth);
            var next = FindNextNonRemovedClass(codepoints, i + 1, codepoints[i].IsolateDepth);
            if (bidiClass == BidiClass.ES && previous == BidiClass.EN && next == BidiClass.EN)
            {
                codepoints[i].ResolvedClass = BidiClass.EN;
            }
            else if (bidiClass == BidiClass.CS && previous == next && previous is BidiClass.EN or BidiClass.AN)
            {
                codepoints[i].ResolvedClass = previous.Value;
            }
        }

        for (var i = 0; i < codepoints.Count; i++)
        {
            if (codepoints[i].ResolvedClass == BidiClass.ET)
            {
                var start = i;
                while (i + 1 < codepoints.Count && codepoints[i + 1].ResolvedClass == BidiClass.ET)
                {
                    i++;
                }

                var previous = FindPreviousNonRemovedClass(codepoints, start - 1, codepoints[start].IsolateDepth);
                var next = FindNextNonRemovedClass(codepoints, i + 1, codepoints[start].IsolateDepth);
                if (previous == BidiClass.EN || next == BidiClass.EN)
                {
                    for (var j = start; j <= i; j++)
                    {
                        codepoints[j].ResolvedClass = BidiClass.EN;
                    }
                }
            }
        }

        for (var i = 0; i < codepoints.Count; i++)
        {
            if (codepoints[i].ResolvedClass is BidiClass.ES or BidiClass.ET or BidiClass.CS)
            {
                codepoints[i].ResolvedClass = BidiClass.ON;
            }
        }

        for (var i = 0; i < codepoints.Count; i++)
        {
            if (codepoints[i].ResolvedClass == BidiClass.EN &&
                FindPreviousStrongClass(codepoints, i - 1, codepoints[i].IsolateDepth) == BidiClass.L)
            {
                codepoints[i].ResolvedClass = BidiClass.L;
            }
        }
    }

    private static void ResolveNeutralClasses(IReadOnlyList<BidiCodepoint> codepoints, SvgTextDirection baseDirection)
    {
        _ = baseDirection;
        ResolvePairedBracketClasses(codepoints);
        for (var i = 0; i < codepoints.Count; i++)
        {
            if (!codepoints[i].IsBoundaryNeutral)
            {
                continue;
            }

            var start = i;
            var isolateDepth = codepoints[i].IsolateDepth;
            while (i + 1 < codepoints.Count &&
                   codepoints[i + 1].IsBoundaryNeutral &&
                   codepoints[i + 1].IsolateDepth == isolateDepth)
            {
                i++;
            }

            var previous = FindPreviousStrongOrNumberClass(codepoints, start - 1, isolateDepth);
            var next = FindNextStrongOrNumberClass(codepoints, i + 1, isolateDepth);
            var resolved = previous.HasValue && next.HasValue && previous.Value == next.Value
                ? previous.Value
                : StrongClassFromLevel(codepoints[start].Level);
            for (var j = start; j <= i; j++)
            {
                if (codepoints[j].ResolvedClass != BidiClass.BN)
                {
                    codepoints[j].ResolvedClass = resolved;
                }
            }
        }
    }

    private static void ResolvePairedBracketClasses(IReadOnlyList<BidiCodepoint> codepoints)
    {
        var stack = new List<int>();
        var pairs = new List<BracketPair>();
        for (var i = 0; i < codepoints.Count; i++)
        {
            if (!TryGetPairedBracket(codepoints[i].Scalar, out var pair, out var isOpening))
            {
                continue;
            }

            if (isOpening)
            {
                stack.Add(i);
                continue;
            }

            for (var stackIndex = stack.Count - 1; stackIndex >= 0; stackIndex--)
            {
                var openIndex = stack[stackIndex];
                if (codepoints[openIndex].Scalar != pair ||
                    codepoints[openIndex].IsolateDepth != codepoints[i].IsolateDepth)
                {
                    continue;
                }

                pairs.Add(new BracketPair(openIndex, i));
                stack.RemoveAt(stackIndex);
                break;
            }
        }

        for (var i = 0; i < pairs.Count; i++)
        {
            ResolvePairedBracketClass(codepoints, pairs[i]);
        }
    }

    private static void ResolvePairedBracketClass(IReadOnlyList<BidiCodepoint> codepoints, BracketPair pair)
    {
        var embeddingClass = StrongClassFromLevel(codepoints[pair.OpenIndex].Level);
        var oppositeClass = embeddingClass == BidiClass.L ? BidiClass.R : BidiClass.L;
        var hasEmbeddingStrong = false;
        var hasOppositeStrong = false;
        for (var i = pair.OpenIndex + 1; i < pair.CloseIndex; i++)
        {
            if (codepoints[i].IsolateDepth != codepoints[pair.OpenIndex].IsolateDepth)
            {
                continue;
            }

            var strongClass = StrongClass(codepoints[i].ResolvedClass);
            if (!strongClass.HasValue)
            {
                continue;
            }

            if (strongClass.Value == embeddingClass)
            {
                hasEmbeddingStrong = true;
                break;
            }

            if (strongClass.Value == oppositeClass)
            {
                hasOppositeStrong = true;
            }
        }

        BidiClass? resolvedClass = null;
        if (hasEmbeddingStrong)
        {
            resolvedClass = embeddingClass;
        }
        else if (hasOppositeStrong)
        {
            var previousStrong = FindPreviousStrongClass(codepoints, pair.OpenIndex - 1, codepoints[pair.OpenIndex].IsolateDepth);
            resolvedClass = previousStrong == oppositeClass ? oppositeClass : embeddingClass;
        }

        if (resolvedClass.HasValue)
        {
            codepoints[pair.OpenIndex].ResolvedClass = resolvedClass.Value;
            codepoints[pair.CloseIndex].ResolvedClass = resolvedClass.Value;
        }
    }

    private static void ResolveImplicitLevels(IReadOnlyList<BidiCodepoint> codepoints)
    {
        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i];
            if (codepoint.ResolvedClass == BidiClass.BN)
            {
                continue;
            }

            var oddLevel = (codepoint.Level & 1) == 1;
            if (oddLevel)
            {
                if (codepoint.ResolvedClass is BidiClass.L or BidiClass.EN or BidiClass.AN)
                {
                    codepoint.Level++;
                }
            }
            else
            {
                if (codepoint.ResolvedClass == BidiClass.R)
                {
                    codepoint.Level++;
                }
                else if (codepoint.ResolvedClass is BidiClass.EN or BidiClass.AN)
                {
                    codepoint.Level += 2;
                }
            }
        }
    }

    private static List<SvgTextBidiRun> CreateLogicalRuns(IReadOnlyList<BidiCodepoint> codepoints, SvgUnicodeBidiMode mode)
    {
        var runs = new List<SvgTextBidiRun>();
        var currentStart = 0;
        var currentLevel = codepoints[0].Level;
        var currentDirection = GetRunDirection(codepoints[0]);
        var splitEveryCodepoint = mode is SvgUnicodeBidiMode.BidiOverride or SvgUnicodeBidiMode.IsolateOverride;

        for (var i = 1; i < codepoints.Count; i++)
        {
            var direction = GetRunDirection(codepoints[i]);
            var mustSplit = splitEveryCodepoint ||
                            codepoints[i].ForcedOverride ||
                            codepoints[i - 1].ForcedOverride;
            if (!mustSplit && codepoints[i].Level == currentLevel && direction == currentDirection)
            {
                continue;
            }

            AddRunIfNotEmpty(runs, codepoints, currentStart, i - 1, currentDirection, currentLevel);
            currentStart = i;
            currentDirection = direction;
            currentLevel = codepoints[i].Level;
        }

        AddRunIfNotEmpty(runs, codepoints, currentStart, codepoints.Count - 1, currentDirection, currentLevel);
        return runs;
    }

    private static void AddRunIfNotEmpty(
        List<SvgTextBidiRun> runs,
        IReadOnlyList<BidiCodepoint> codepoints,
        int startCodepointIndex,
        int endCodepointIndex,
        SvgTextDirection direction,
        int level)
    {
        var run = CreateRun(codepoints, startCodepointIndex, endCodepointIndex, direction, level);
        if (run.Length > 0)
        {
            runs.Add(run);
        }
    }

    private static SvgTextBidiRun CreateRun(
        IReadOnlyList<BidiCodepoint> codepoints,
        int startCodepointIndex,
        int endCodepointIndex,
        SvgTextDirection direction,
        int level)
    {
        var startCharIndex = codepoints[startCodepointIndex].CharIndex;
        var endCodepoint = codepoints[endCodepointIndex];
        var endCharIndex = endCodepoint.CharIndex + endCodepoint.CharLength;
        return new SvgTextBidiRun(startCharIndex, endCharIndex - startCharIndex, direction, level);
    }

    private static List<SvgTextBidiRun> ReorderRunsVisually(IReadOnlyList<SvgTextBidiRun> logicalRuns, SvgTextDirection baseDirection)
    {
        var visualRuns = logicalRuns.ToList();
        var maxLevel = visualRuns.Max(static run => run.Level);
        var minOddLevel = visualRuns
            .Select(static run => run.Level)
            .Where(static level => (level & 1) == 1)
            .DefaultIfEmpty(baseDirection == SvgTextDirection.RightToLeft ? 1 : 0)
            .Min();

        for (var level = maxLevel; level >= minOddLevel; level--)
        {
            var start = -1;
            for (var i = 0; i <= visualRuns.Count; i++)
            {
                if (i < visualRuns.Count && visualRuns[i].Level >= level)
                {
                    if (start < 0)
                    {
                        start = i;
                    }

                    continue;
                }

                if (start >= 0 && i - start > 1)
                {
                    visualRuns.Reverse(start, i - start);
                }

                start = -1;
            }
        }

        return visualRuns;
    }

    private static SvgTextDirection GetRunDirection(BidiCodepoint codepoint)
    {
        if ((codepoint.Level & 1) == 1)
        {
            return SvgTextDirection.RightToLeft;
        }

        return codepoint.ResolvedClass == BidiClass.R
            ? SvgTextDirection.RightToLeft
            : SvgTextDirection.LeftToRight;
    }

    private static BidiClass? FindPreviousStrongOrNumberClass(IReadOnlyList<BidiCodepoint> codepoints, int index, int isolateDepth)
    {
        for (var i = index; i >= 0; i--)
        {
            if (codepoints[i].IsolateDepth != isolateDepth)
            {
                continue;
            }

            var bidiClass = StrongOrNumberClass(codepoints[i].ResolvedClass);
            if (bidiClass.HasValue)
            {
                return bidiClass;
            }
        }

        return null;
    }

    private static BidiClass? FindNextStrongOrNumberClass(IReadOnlyList<BidiCodepoint> codepoints, int index, int isolateDepth)
    {
        for (var i = index; i < codepoints.Count; i++)
        {
            if (codepoints[i].IsolateDepth != isolateDepth)
            {
                continue;
            }

            var bidiClass = StrongOrNumberClass(codepoints[i].ResolvedClass);
            if (bidiClass.HasValue)
            {
                return bidiClass;
            }
        }

        return null;
    }

    private static BidiClass? StrongOrNumberClass(BidiClass bidiClass)
    {
        return bidiClass switch
        {
            BidiClass.L => BidiClass.L,
            BidiClass.R or BidiClass.AL => BidiClass.R,
            BidiClass.EN or BidiClass.AN => BidiClass.R,
            _ => null
        };
    }

    private static BidiClass? FindPreviousStrongClass(IReadOnlyList<BidiCodepoint> codepoints, int index, int isolateDepth)
    {
        for (var i = index; i >= 0; i--)
        {
            if (codepoints[i].IsolateDepth != isolateDepth)
            {
                continue;
            }

            var strongClass = StrongClass(codepoints[i].ResolvedClass);
            if (strongClass.HasValue)
            {
                return strongClass.Value;
            }
        }

        return null;
    }

    private static BidiClass? FindPreviousNonRemovedClass(IReadOnlyList<BidiCodepoint> codepoints, int index, int isolateDepth)
    {
        for (var i = index; i >= 0; i--)
        {
            if (codepoints[i].IsolateDepth != isolateDepth ||
                codepoints[i].ResolvedClass == BidiClass.BN)
            {
                continue;
            }

            return codepoints[i].ResolvedClass;
        }

        return null;
    }

    private static BidiClass? FindNextNonRemovedClass(IReadOnlyList<BidiCodepoint> codepoints, int index, int isolateDepth)
    {
        for (var i = index; i < codepoints.Count; i++)
        {
            if (codepoints[i].IsolateDepth != isolateDepth ||
                codepoints[i].ResolvedClass == BidiClass.BN)
            {
                continue;
            }

            return codepoints[i].ResolvedClass;
        }

        return null;
    }

    private static BidiClass? StrongClass(BidiClass bidiClass)
    {
        return bidiClass switch
        {
            BidiClass.L => BidiClass.L,
            BidiClass.R or BidiClass.AL => BidiClass.R,
            _ => null
        };
    }

    private static SvgTextDirection ResolveFirstStrongIsolateDirection(IReadOnlyList<BidiCodepoint> codepoints, int startIndex, SvgTextDirection fallback)
    {
        var depth = 0;
        for (var i = startIndex; i < codepoints.Count; i++)
        {
            var bidiClass = codepoints[i].BidiClass;
            if (bidiClass is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI)
            {
                depth++;
                continue;
            }

            if (bidiClass == BidiClass.PDI)
            {
                if (depth == 0)
                {
                    break;
                }

                depth--;
                continue;
            }

            if (depth > 0)
            {
                continue;
            }

            var strongDirection = GetStrongDirection(bidiClass);
            if (strongDirection.HasValue)
            {
                return strongDirection.Value;
            }
        }

        return fallback;
    }

    private static int GetNextEmbeddingLevel(int currentLevel, SvgTextDirection direction)
    {
        var nextLevel = currentLevel + 1;
        if (direction == SvgTextDirection.LeftToRight && (nextLevel & 1) == 1)
        {
            nextLevel++;
        }
        else if (direction == SvgTextDirection.RightToLeft && (nextLevel & 1) == 0)
        {
            nextLevel++;
        }

        return nextLevel;
    }

    private static BidiClass ToStrongClass(SvgTextDirection direction)
    {
        return direction == SvgTextDirection.RightToLeft ? BidiClass.R : BidiClass.L;
    }

    private static BidiClass StrongClassFromLevel(int level)
    {
        return (level & 1) == 1 ? BidiClass.R : BidiClass.L;
    }

    private static SvgTextDirection? GetStrongDirection(BidiClass bidiClass)
    {
        return bidiClass switch
        {
            BidiClass.L or BidiClass.LRE or BidiClass.LRO or BidiClass.LRI => SvgTextDirection.LeftToRight,
            BidiClass.R or BidiClass.AL or BidiClass.RLE or BidiClass.RLO or BidiClass.RLI => SvgTextDirection.RightToLeft,
            _ => null
        };
    }

    private static bool IsExplicitBidiClass(BidiClass bidiClass)
    {
        return bidiClass is BidiClass.LRE or BidiClass.RLE or BidiClass.LRO or BidiClass.RLO or BidiClass.PDF or
            BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI;
    }

    private static string ReverseByCodepoint(string text)
    {
        var codepoints = CreateCodepoints(text);
        codepoints.Reverse();
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < codepoints.Count; i++)
        {
            builder.Append(codepoints[i].Text);
        }

        return builder.ToString();
    }

    private static bool TryGetPairedBracket(int scalar, out int pair, out bool isOpening)
    {
        isOpening = true;
        pair = scalar switch
        {
            0x0028 => 0x0029,
            0x005B => 0x005D,
            0x007B => 0x007D,
            0x0F3A => 0x0F3B,
            0x0F3C => 0x0F3D,
            0x169B => 0x169C,
            0x2045 => 0x2046,
            0x207D => 0x207E,
            0x208D => 0x208E,
            0x2308 => 0x2309,
            0x230A => 0x230B,
            0x2329 => 0x232A,
            0x2768 => 0x2769,
            0x276A => 0x276B,
            0x276C => 0x276D,
            0x276E => 0x276F,
            0x2770 => 0x2771,
            0x2772 => 0x2773,
            0x2774 => 0x2775,
            0x27C5 => 0x27C6,
            0x27E6 => 0x27E7,
            0x27E8 => 0x27E9,
            0x27EA => 0x27EB,
            0x27EC => 0x27ED,
            0x27EE => 0x27EF,
            0x2983 => 0x2984,
            0x2985 => 0x2986,
            0x2987 => 0x2988,
            0x2989 => 0x298A,
            0x298B => 0x298C,
            0x298D => 0x2990,
            0x298F => 0x298E,
            0x2991 => 0x2992,
            0x2993 => 0x2994,
            0x2995 => 0x2996,
            0x2997 => 0x2998,
            0x29D8 => 0x29D9,
            0x29DA => 0x29DB,
            0x29FC => 0x29FD,
            0x2E22 => 0x2E23,
            0x2E24 => 0x2E25,
            0x2E26 => 0x2E27,
            0x2E28 => 0x2E29,
            0x3008 => 0x3009,
            0x300A => 0x300B,
            0x300C => 0x300D,
            0x300E => 0x300F,
            0x3010 => 0x3011,
            0x3014 => 0x3015,
            0x3016 => 0x3017,
            0x3018 => 0x3019,
            0x301A => 0x301B,
            0xFE59 => 0xFE5A,
            0xFE5B => 0xFE5C,
            0xFE5D => 0xFE5E,
            0xFF08 => 0xFF09,
            0xFF3B => 0xFF3D,
            0xFF5B => 0xFF5D,
            0xFF5F => 0xFF60,
            0xFF62 => 0xFF63,
            _ => -1
        };

        if (pair >= 0)
        {
            return true;
        }

        isOpening = false;
        pair = scalar switch
        {
            0x0029 => 0x0028,
            0x005D => 0x005B,
            0x007D => 0x007B,
            0x0F3B => 0x0F3A,
            0x0F3D => 0x0F3C,
            0x169C => 0x169B,
            0x2046 => 0x2045,
            0x207E => 0x207D,
            0x208E => 0x208D,
            0x2309 => 0x2308,
            0x230B => 0x230A,
            0x232A => 0x2329,
            0x2769 => 0x2768,
            0x276B => 0x276A,
            0x276D => 0x276C,
            0x276F => 0x276E,
            0x2771 => 0x2770,
            0x2773 => 0x2772,
            0x2775 => 0x2774,
            0x27C6 => 0x27C5,
            0x27E7 => 0x27E6,
            0x27E9 => 0x27E8,
            0x27EB => 0x27EA,
            0x27ED => 0x27EC,
            0x27EF => 0x27EE,
            0x2984 => 0x2983,
            0x2986 => 0x2985,
            0x2988 => 0x2987,
            0x298A => 0x2989,
            0x298C => 0x298B,
            0x298E => 0x298F,
            0x2990 => 0x298D,
            0x2992 => 0x2991,
            0x2994 => 0x2993,
            0x2996 => 0x2995,
            0x2998 => 0x2997,
            0x29D9 => 0x29D8,
            0x29DB => 0x29DA,
            0x29FD => 0x29FC,
            0x2E23 => 0x2E22,
            0x2E25 => 0x2E24,
            0x2E27 => 0x2E26,
            0x2E29 => 0x2E28,
            0x3009 => 0x3008,
            0x300B => 0x300A,
            0x300D => 0x300C,
            0x300F => 0x300E,
            0x3011 => 0x3010,
            0x3015 => 0x3014,
            0x3017 => 0x3016,
            0x3019 => 0x3018,
            0x301B => 0x301A,
            0xFE5A => 0xFE59,
            0xFE5C => 0xFE5B,
            0xFE5E => 0xFE5D,
            0xFF09 => 0xFF08,
            0xFF3D => 0xFF3B,
            0xFF5D => 0xFF5B,
            0xFF60 => 0xFF5F,
            0xFF63 => 0xFF62,
            _ => -1
        };

        return pair >= 0;
    }

    private static BidiClass GetBidiClass(int scalar)
    {
        return scalar switch
        {
            0x000A or 0x000D or 0x0085 or 0x2029 => BidiClass.B,
            0x0009 or 0x000B or 0x001F => BidiClass.S,
            0x0020 or 0x00A0 or 0x1680 or 0x202F or 0x205F or 0x3000 => BidiClass.WS,
            >= 0x2000 and <= 0x200A => BidiClass.WS,
            0x061C => BidiClass.AL,
            0x200E => BidiClass.L,
            0x200F => BidiClass.R,
            0x202A => BidiClass.LRE,
            0x202B => BidiClass.RLE,
            0x202C => BidiClass.PDF,
            0x202D => BidiClass.LRO,
            0x202E => BidiClass.RLO,
            0x2066 => BidiClass.LRI,
            0x2067 => BidiClass.RLI,
            0x2068 => BidiClass.FSI,
            0x2069 => BidiClass.PDI,
            >= '0' and <= '9' => BidiClass.EN,
            >= 0x0660 and <= 0x0669 => BidiClass.AN,
            >= 0x06F0 and <= 0x06F9 => BidiClass.AN,
            '+' or '-' => BidiClass.ES,
            ',' or '.' or '/' or ':' => BidiClass.CS,
            '$' or '%' or '#' or 0x00A2 or 0x00A3 or 0x00A4 or 0x00A5 => BidiClass.ET,
            >= 0x0590 and <= 0x05FF => BidiClass.R,
            >= 0x0600 and <= 0x08FF => BidiClass.AL,
            >= 0xFB1D and <= 0xFDFF => BidiClass.AL,
            >= 0xFE70 and <= 0xFEFF => BidiClass.AL,
            >= 0x10800 and <= 0x10FFF => BidiClass.R,
            >= 0x1E800 and <= 0x1EEFF => BidiClass.AL,
            _ => GetBidiClassFromUnicodeCategory(scalar)
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static BidiClass GetBidiClassFromUnicodeCategory(int scalar)
    {
        var text = char.ConvertFromUtf32(scalar);
        var category = CharUnicodeInfo.GetUnicodeCategory(text, 0);
        return category switch
        {
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark => BidiClass.NSM,
            UnicodeCategory.Format or UnicodeCategory.Control => BidiClass.BN,
            UnicodeCategory.DecimalDigitNumber => BidiClass.EN,
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.LetterNumber => BidiClass.L,
            _ => BidiClass.ON
        };
    }
}
