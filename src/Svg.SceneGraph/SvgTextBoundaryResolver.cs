#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Svg.Skia;

internal interface ISvgTextDictionaryBreakProvider
{
    bool TryResolveComplexContextBreak(
        IReadOnlyList<string> codepoints,
        int index,
        out bool allowsBreakAfter);
}

internal interface ISvgTextBoundaryResolver : ISvgTextLayoutBreakResolver
{
    bool TryGetBreakOpportunity(
        IReadOnlyList<string> codepoints,
        int index,
        SvgTextLineBreakOptions options,
        bool insideBidiFormatting,
        string? previousCodepoint,
        string? nextCodepoint,
        bool isClusterBoundaryAfter,
        out SvgTextBreakOpportunity opportunity);

    bool AllowsSoftWrapOpportunity(
        IReadOnlyList<string> codepoints,
        int index,
        SvgTextLineBreakOptions options);

    bool StartsNewGraphemeCluster(
        int previousScalar,
        int currentScalar,
        int regionalIndicatorRunLength);

    IReadOnlyList<int> GetGraphemeClusterStartCharIndexes(string text);

    bool IsGraphemeClusterBoundary(
        IReadOnlyList<string> codepoints,
        int beforeCodepointIndex,
        int afterCodepointIndex);

    bool IsGraphemeClusterExtender(int scalar);

    bool IsCombiningOrJoiningCodepoint(int scalar);

    bool IsNoBreakAdjacentFormatControl(string? codepoint);

    bool IsBreakOpportunityWhitespace(
        string codepoint,
        string? previousCodepoint = null,
        string? nextCodepoint = null,
        bool insideBidiFormatting = false);

    bool IsInvisibleBreakOpportunity(
        string codepoint,
        string? previousCodepoint = null,
        string? nextCodepoint = null,
        bool insideBidiFormatting = false);

    int UpdateBidiFormattingDepth(int depth, string codepoint);
}

internal sealed class SvgTextBoundaryResolver : ISvgTextBoundaryResolver
{
    private readonly ISvgTextDictionaryBreakProvider? _dictionaryBreakProvider;

    private enum LineBreakClass
    {
        Alphabetic,
        Numeric,
        Ideographic,
        ComplexContext,
        Combining,
        ZeroWidthJoiner,
        Glue,
        WordJoiner,
        Opening,
        Closing,
        ClosingParenthesis,
        BreakAfter,
        BreakBefore,
        BreakBoth,
        Hyphen,
        InfixNumeric,
        SymbolBreakAfter,
        Exclamation,
        Nonstarter,
        PrefixNumeric,
        PostfixNumeric,
        HangulL,
        HangulV,
        HangulT,
        HangulLv,
        HangulLvt,
        RegionalIndicator,
        Other
    }

    public SvgTextBoundaryResolver(ISvgTextDictionaryBreakProvider? dictionaryBreakProvider = null)
    {
        _dictionaryBreakProvider = dictionaryBreakProvider;
    }

    public static SvgTextBoundaryResolver Default { get; } = new();

    public bool TryGetBreakOpportunity(
        IReadOnlyList<string> codepoints,
        int index,
        SvgTextLineBreakOptions options,
        bool insideBidiFormatting,
        string? previousCodepoint,
        string? nextCodepoint,
        bool isClusterBoundaryAfter,
        out SvgTextBreakOpportunity opportunity)
    {
        opportunity = default;
        if (index < 0 || index >= codepoints.Count)
        {
            return false;
        }

        var codepoint = codepoints[index];
        previousCodepoint ??= index > 0 ? codepoints[index - 1] : null;
        nextCodepoint ??= index + 1 < codepoints.Count ? codepoints[index + 1] : null;

        if (codepoint == "\n")
        {
            opportunity = new SvgTextBreakOpportunity(
                index - 1,
                index + 1,
                index,
                SvgTextBreakOpportunityKind.ForcedLine,
                SvgTextBreakPriority.Forced,
                ConsumesCodepoint: true);
            return true;
        }

        if (IsInvisibleBreakOpportunity(codepoint, previousCodepoint, nextCodepoint, insideBidiFormatting) &&
            IsClusterBoundaryAroundInvisibleBreak(codepoints, index))
        {
            opportunity = new SvgTextBreakOpportunity(
                index - 1,
                index + 1,
                index,
                SvgTextBreakOpportunityKind.Invisible,
                SvgTextBreakPriority.Natural,
                ConsumesCodepoint: true);
            return true;
        }

        if (IsBreakOpportunityWhitespace(codepoint, previousCodepoint, nextCodepoint, insideBidiFormatting))
        {
            opportunity = new SvgTextBreakOpportunity(
                index,
                index + 1,
                index,
                SvgTextBreakOpportunityKind.Whitespace,
                SvgTextBreakPriority.Whitespace,
                ConsumesCodepoint: false);
            return true;
        }

        if (!isClusterBoundaryAfter ||
            insideBidiFormatting ||
            IsNoBreakAdjacentFormatControl(previousCodepoint) ||
            IsNoBreakAdjacentFormatControl(nextCodepoint))
        {
            return false;
        }

        if (!AllowsSoftWrapOpportunity(codepoints, index, options))
        {
            if (!AllowsClusterEmergencyBreak(codepoints, index, options, nextCodepoint))
            {
                return false;
            }

            opportunity = new SvgTextBreakOpportunity(
                index,
                index + 1,
                -1,
                SvgTextBreakOpportunityKind.Soft,
                options.LineBreakAnywhere ? SvgTextBreakPriority.Soft : SvgTextBreakPriority.Emergency,
                ConsumesCodepoint: false);
            return true;
        }

        var naturalOptions = options.WithoutEmergencyBreaks();
        var natural = AllowsSoftWrapOpportunity(codepoints, index, naturalOptions);
        opportunity = new SvgTextBreakOpportunity(
            index,
            index + 1,
            -1,
            SvgTextBreakOpportunityKind.Soft,
            natural || options.LineBreakAnywhere ? SvgTextBreakPriority.Soft : SvgTextBreakPriority.Emergency,
            ConsumesCodepoint: false);
        return true;
    }

    public bool AllowsSoftWrapOpportunity(
        SvgTextCodepointRun run,
        int codepointIndex,
        SvgTextLineBreakPolicy policy)
    {
        if (run is null)
        {
            throw new ArgumentNullException(nameof(run));
        }

        var codepoints = new string[run.Codepoints.Count];
        for (var i = 0; i < run.Codepoints.Count; i++)
        {
            codepoints[i] = run.Codepoints[i].Text;
        }

        return AllowsSoftWrapOpportunity(codepoints, codepointIndex, policy.ToLineBreakOptions());
    }

    public bool AllowsSoftWrapOpportunity(
        IReadOnlyList<string> codepoints,
        int index,
        SvgTextLineBreakOptions options)
    {
        if (index < 0 || index + 1 >= codepoints.Count)
        {
            return false;
        }

        var previousScalar = index > 0 ? GetCodepointScalar(codepoints[index - 1]) : -1;
        var currentScalar = GetCodepointScalar(codepoints[index]);
        var nextScalar = GetCodepointScalar(codepoints[index + 1]);
        if (currentScalar < 0 || nextScalar < 0)
        {
            return false;
        }

        var currentClass = GetLineBreakClass(currentScalar);
        var nextClass = GetLineBreakClass(nextScalar);
        if (!IsTypographicBreakBoundary(currentScalar, nextScalar) ||
            IsHardNoBreakBoundary(currentClass, nextClass))
        {
            return false;
        }

        if (options.LineBreakAnywhere)
        {
            return true;
        }

        if (options.OverflowWrapAnywhere)
        {
            return !IsEmergencyBreakSuppressed(currentClass, nextClass);
        }

        if (options.WordBreakBreakAll)
        {
            return !IsEmergencyBreakSuppressed(currentClass, nextClass) &&
                   !IsBreakProhibitedAfter(currentClass, currentScalar, options) &&
                   !IsBreakProhibitedBefore(nextClass, nextScalar, options);
        }

        if (options.WordBreakKeepAll &&
            (IsKeepAllBreakClass(currentClass) || IsKeepAllBreakClass(nextClass)))
        {
            return false;
        }

        if (IsComplexContextBreak(codepoints, index, currentClass, nextClass, out var allowsComplexContextBreak))
        {
            return allowsComplexContextBreak;
        }

        if (IsWordInitialHyphenBreak(previousScalar, currentClass, nextClass) ||
            IsNumericExpressionNoBreak(previousScalar, currentScalar, nextScalar, currentClass, nextClass) ||
            IsApostropheNoBreak(previousScalar, currentScalar, nextScalar, currentClass, nextClass) ||
            IsNonEastAsianClosingToAlnumNoBreak(currentClass, nextClass) ||
            IsAlnumToNonEastAsianOpeningNoBreak(currentClass, nextClass) ||
            IsHangulSyllableNoBreak(currentClass, nextClass) ||
            IsRegionalIndicatorPairNoBreak(codepoints, index, currentClass, nextClass) ||
            IsBreakProhibitedAfter(currentClass, currentScalar, options) ||
            IsBreakProhibitedBefore(nextClass, nextScalar, options) ||
            IsAlphabeticNoBreak(currentClass, nextClass))
        {
            return false;
        }

        return currentClass is LineBreakClass.Ideographic or
                              LineBreakClass.ComplexContext or
                              LineBreakClass.Closing or
                              LineBreakClass.ClosingParenthesis or
                              LineBreakClass.BreakAfter or
                              LineBreakClass.Hyphen or
                              LineBreakClass.InfixNumeric or
                              LineBreakClass.SymbolBreakAfter or
                              LineBreakClass.Exclamation or
                              LineBreakClass.Nonstarter or
                              LineBreakClass.PostfixNumeric ||
               nextClass is LineBreakClass.Ideographic or
                            LineBreakClass.ComplexContext or
                            LineBreakClass.BreakBefore or
                            LineBreakClass.PrefixNumeric;
    }

    public bool StartsNewGraphemeCluster(
        int previousScalar,
        int currentScalar,
        int regionalIndicatorRunLength)
    {
        if (previousScalar == '\n' || currentScalar == '\n')
        {
            return true;
        }

        if (currentScalar == 0x200D ||
            previousScalar == 0x200D ||
            IsPrependCodepoint(previousScalar) ||
            IsGraphemeClusterExtender(currentScalar) ||
            IsHangulGraphemeClusterContinuation(previousScalar, currentScalar))
        {
            return false;
        }

        if (IsRegionalIndicator(previousScalar) && IsRegionalIndicator(currentScalar))
        {
            return regionalIndicatorRunLength % 2 == 0;
        }

        return true;
    }

    public IReadOnlyList<int> GetGraphemeClusterStartCharIndexes(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<int>();
        }

        var starts = new List<int> { 0 };
        var charIndex = 0;
        var previousScalar = char.ConvertToUtf32(text, charIndex);
        charIndex += previousScalar > 0xFFFF ? 2 : 1;
        var regionalIndicatorRunLength = IsRegionalIndicator(previousScalar) ? 1 : 0;

        while (charIndex < text.Length)
        {
            var currentCharIndex = charIndex;
            var currentScalar = char.ConvertToUtf32(text, charIndex);
            charIndex += currentScalar > 0xFFFF ? 2 : 1;

            if (StartsNewGraphemeCluster(previousScalar, currentScalar, regionalIndicatorRunLength))
            {
                starts.Add(currentCharIndex);
            }

            regionalIndicatorRunLength = IsRegionalIndicator(currentScalar)
                ? starts[starts.Count - 1] == currentCharIndex ? 1 : regionalIndicatorRunLength + 1
                : 0;
            previousScalar = currentScalar;
        }

        return starts;
    }

    public bool IsGraphemeClusterBoundary(
        IReadOnlyList<string> codepoints,
        int beforeCodepointIndex,
        int afterCodepointIndex)
    {
        if (beforeCodepointIndex < 0 ||
            afterCodepointIndex != beforeCodepointIndex + 1 ||
            afterCodepointIndex >= codepoints.Count)
        {
            return false;
        }

        var previousScalar = GetCodepointScalar(codepoints[beforeCodepointIndex]);
        var currentScalar = GetCodepointScalar(codepoints[afterCodepointIndex]);
        if (previousScalar < 0 || currentScalar < 0)
        {
            return false;
        }

        var regionalIndicatorRunLength = 0;
        for (var i = beforeCodepointIndex; i >= 0; i--)
        {
            if (!IsRegionalIndicator(GetCodepointScalar(codepoints[i])))
            {
                break;
            }

            regionalIndicatorRunLength++;
        }

        return StartsNewGraphemeCluster(previousScalar, currentScalar, regionalIndicatorRunLength);
    }

    public bool IsGraphemeClusterExtender(int scalar)
    {
        if (scalar >= 0xFE00 && scalar <= 0xFE0F ||
            scalar >= 0xE0100 && scalar <= 0xE01EF ||
            scalar >= 0x1F3FB && scalar <= 0x1F3FF ||
            scalar >= 0xE0020 && scalar <= 0xE007F)
        {
            return true;
        }

        return IsCombiningOrJoiningCodepoint(scalar);
    }

    public bool IsCombiningOrJoiningCodepoint(int scalar)
    {
        if (scalar is 0x200C or 0x200D or 0x2060 or 0xFEFF ||
            scalar >= 0xFE00 && scalar <= 0xFE0F ||
            scalar >= 0xE0100 && scalar <= 0xE01EF ||
            scalar >= 0x1F3FB && scalar <= 0x1F3FF ||
            scalar >= 0xE0020 && scalar <= 0xE007F)
        {
            return true;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(scalar), 0);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
    }

    public bool IsBreakOpportunityWhitespace(
        string codepoint,
        string? previousCodepoint = null,
        string? nextCodepoint = null,
        bool insideBidiFormatting = false)
    {
        if (!IsWhitespaceCodepoint(codepoint) ||
            insideBidiFormatting ||
            IsNoBreakAdjacentFormatControl(previousCodepoint) ||
            IsNoBreakAdjacentFormatControl(nextCodepoint))
        {
            return false;
        }

        return codepoint is not "\u00A0" and not "\u2007" and not "\u202F" and not "\u2060" and not "\uFEFF";
    }

    public bool IsInvisibleBreakOpportunity(
        string codepoint,
        string? previousCodepoint = null,
        string? nextCodepoint = null,
        bool insideBidiFormatting = false)
    {
        if (string.IsNullOrEmpty(codepoint) ||
            insideBidiFormatting ||
            IsNoBreakAdjacentFormatControl(previousCodepoint) ||
            IsNoBreakAdjacentFormatControl(nextCodepoint))
        {
            return false;
        }

        var scalar = char.ConvertToUtf32(codepoint, 0);
        return scalar is 0x00AD or 0x200B;
    }

    public int UpdateBidiFormattingDepth(int depth, string codepoint)
    {
        return codepoint switch
        {
            "\u202A" or "\u202B" or "\u202D" or "\u202E" or "\u2066" or "\u2067" or "\u2068" => depth + 1,
            "\u202C" or "\u2069" => Math.Max(0, depth - 1),
            _ => depth
        };
    }

    public bool IsNoBreakAdjacentFormatControl(string? codepoint)
    {
        return codepoint is "\u034F" or "\u061C" or "\u200C" or "\u200D" or "\u200E" or "\u200F" or
            "\u202A" or "\u202B" or "\u202C" or "\u202D" or "\u202E" or "\u2060" or
            "\u2066" or "\u2067" or "\u2068" or "\u2069" or "\uFEFF";
    }

    private bool AllowsClusterEmergencyBreak(
        IReadOnlyList<string> codepoints,
        int index,
        SvgTextLineBreakOptions options,
        string? nextCodepoint)
    {
        if (!options.AllowsCharacterBreaks ||
            index < 0 ||
            index + 1 >= codepoints.Count ||
            string.IsNullOrEmpty(nextCodepoint) ||
            !IsCodepointClusterExtender(codepoints[index]))
        {
            return false;
        }

        var next = nextCodepoint!;
        if (IsHardNoBreakFormatCodepoint(codepoints[index]) ||
            IsHardNoBreakFormatCodepoint(next) ||
            IsCodepointClusterExtender(next))
        {
            return false;
        }

        var nextScalar = GetCodepointScalar(next);
        return nextScalar >= 0 && !IsCombiningOrJoiningCodepoint(nextScalar);
    }

    private static int GetCodepointScalar(string codepoint)
    {
        return string.IsNullOrEmpty(codepoint) ? -1 : char.ConvertToUtf32(codepoint, 0);
    }

    private bool IsTypographicBreakBoundary(int currentScalar, int nextScalar)
    {
        return !IsCombiningOrJoiningCodepoint(currentScalar) &&
               !IsCombiningOrJoiningCodepoint(nextScalar) &&
               currentScalar != 0x200D &&
               nextScalar != 0x200D;
    }

    private static bool IsEmergencyBreakSuppressed(LineBreakClass currentClass, LineBreakClass nextClass)
    {
        return currentClass is LineBreakClass.Combining or
                               LineBreakClass.ZeroWidthJoiner or
                               LineBreakClass.Glue or
                               LineBreakClass.WordJoiner ||
               nextClass is LineBreakClass.Combining or
                            LineBreakClass.ZeroWidthJoiner or
                            LineBreakClass.Glue or
                            LineBreakClass.WordJoiner;
    }

    private static bool IsHardNoBreakBoundary(LineBreakClass currentClass, LineBreakClass nextClass)
    {
        return currentClass is LineBreakClass.Glue or LineBreakClass.WordJoiner ||
               nextClass is LineBreakClass.Glue or LineBreakClass.WordJoiner;
    }

    private static bool IsHardNoBreakFormatCodepoint(string codepoint)
    {
        return !string.IsNullOrEmpty(codepoint) &&
               char.ConvertToUtf32(codepoint, 0) is 0x200C or 0x200D or 0x2060 or 0xFEFF;
    }

    private LineBreakClass GetLineBreakClass(int scalar)
    {
        if (IsCombiningOrJoiningCodepoint(scalar))
        {
            return scalar == 0x200D
                ? LineBreakClass.ZeroWidthJoiner
                : LineBreakClass.Combining;
        }

        if (IsCjkBreakCodepoint(scalar) ||
            scalar is >= 0x3000 and <= 0x303F or >= 0xFF00 and <= 0xFFEF)
        {
            return ClassifyCjkLineBreakClass(scalar);
        }

        if (IsComplexContextCodepoint(scalar))
        {
            return LineBreakClass.ComplexContext;
        }

        if (IsHangulJamoL(scalar))
        {
            return LineBreakClass.HangulL;
        }

        if (IsHangulJamoV(scalar))
        {
            return LineBreakClass.HangulV;
        }

        if (IsHangulJamoT(scalar))
        {
            return LineBreakClass.HangulT;
        }

        if (IsHangulLvSyllable(scalar))
        {
            return LineBreakClass.HangulLv;
        }

        if (IsHangulLvtSyllable(scalar))
        {
            return LineBreakClass.HangulLvt;
        }

        if (IsRegionalIndicator(scalar))
        {
            return LineBreakClass.RegionalIndicator;
        }

        if (scalar is 0x00A0 or 0x2007 or 0x202F)
        {
            return LineBreakClass.Glue;
        }

        if (scalar is 0x2060 or 0xFEFF)
        {
            return LineBreakClass.WordJoiner;
        }

        if (IsDashBreakCodepoint(scalar))
        {
            return LineBreakClass.Hyphen;
        }

        if (scalar is 0x002F)
        {
            return LineBreakClass.SymbolBreakAfter;
        }

        if (scalar is 0x002C or 0x002E or 0x003A or 0x003B)
        {
            return LineBreakClass.InfixNumeric;
        }

        if (scalar is 0x0021 or 0x003F or 0x203C or 0x2047 or 0x2048 or 0x2049)
        {
            return LineBreakClass.Exclamation;
        }

        if (scalar is 0x0028 or 0x005B or 0x007B)
        {
            return LineBreakClass.Opening;
        }

        if (scalar is 0x0029 or 0x005D or 0x007D)
        {
            return LineBreakClass.ClosingParenthesis;
        }

        if (scalar is 0x0025 or 0x00A2 or 0x00B0 or 0x2030 or 0x2031)
        {
            return LineBreakClass.PostfixNumeric;
        }

        if (scalar is 0x0024 or 0x00A3 or 0x00A5 or 0x20AC)
        {
            return LineBreakClass.PrefixNumeric;
        }

        if (scalar is 0x0026 or 0x002B)
        {
            return LineBreakClass.BreakBefore;
        }

        if (scalar is 0x007C)
        {
            return LineBreakClass.BreakBoth;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(scalar), 0);
        return category switch
        {
            UnicodeCategory.DecimalDigitNumber => LineBreakClass.Numeric,
            UnicodeCategory.OpenPunctuation or UnicodeCategory.InitialQuotePunctuation => LineBreakClass.Opening,
            UnicodeCategory.ClosePunctuation or UnicodeCategory.FinalQuotePunctuation => LineBreakClass.Closing,
            UnicodeCategory.DashPunctuation => LineBreakClass.Hyphen,
            UnicodeCategory.OtherPunctuation => LineBreakClass.BreakAfter,
            UnicodeCategory.CurrencySymbol => LineBreakClass.PrefixNumeric,
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.LetterNumber => LineBreakClass.Alphabetic,
            _ => LineBreakClass.Other
        };
    }

    private static LineBreakClass ClassifyCjkLineBreakClass(int scalar)
    {
        if (scalar is 0x3008 or 0x300A or 0x300C or 0x300E or 0x3010 or 0x3014 or 0x3016 or 0x3018 or 0x301A or
            0xFF08 or 0xFF3B or 0xFF5B)
        {
            return LineBreakClass.Opening;
        }

        if (scalar is 0x3009 or 0x300B or 0x300D or 0x300F or 0x3011 or 0x3015 or 0x3017 or 0x3019 or 0x301B or
            0x301E or 0x301F or 0xFF09 or 0xFF3D or 0xFF5D)
        {
            return LineBreakClass.Closing;
        }

        if (scalar is 0x3001 or 0x3002 or 0xFF0C or 0xFF0E)
        {
            return LineBreakClass.InfixNumeric;
        }

        if (scalar is 0xFF01 or 0xFF1F or 0xFF1A or 0xFF1B)
        {
            return LineBreakClass.Exclamation;
        }

        if (scalar is 0x30FB or 0xFF65)
        {
            return LineBreakClass.BreakAfter;
        }

        if (IsStrictLineBreakProhibitedBefore(scalar))
        {
            return LineBreakClass.Nonstarter;
        }

        return LineBreakClass.Ideographic;
    }

    private static bool IsBreakProhibitedBefore(
        LineBreakClass lineBreakClass,
        int scalar,
        SvgTextLineBreakOptions options)
    {
        if (lineBreakClass is LineBreakClass.Combining or
            LineBreakClass.ZeroWidthJoiner or
            LineBreakClass.Glue or
            LineBreakClass.WordJoiner or
            LineBreakClass.Closing or
            LineBreakClass.ClosingParenthesis or
            LineBreakClass.Exclamation or
            LineBreakClass.InfixNumeric or
            LineBreakClass.SymbolBreakAfter or
            LineBreakClass.PostfixNumeric)
        {
            return true;
        }

        if (options.LineBreakLoose)
        {
            return false;
        }

        return options.StrictLineBreak &&
               (lineBreakClass == LineBreakClass.Nonstarter ||
                IsStrictLineBreakProhibitedBefore(scalar));
    }

    private static bool IsBreakProhibitedAfter(
        LineBreakClass lineBreakClass,
        int scalar,
        SvgTextLineBreakOptions options)
    {
        _ = scalar;
        _ = options;
        return lineBreakClass is LineBreakClass.Combining or
                                  LineBreakClass.ZeroWidthJoiner or
                                  LineBreakClass.Glue or
                                  LineBreakClass.WordJoiner or
                                  LineBreakClass.Opening or
                                  LineBreakClass.PrefixNumeric or
                                  LineBreakClass.BreakBefore;
    }

    private bool IsComplexContextBreak(
        IReadOnlyList<string> codepoints,
        int index,
        LineBreakClass currentClass,
        LineBreakClass nextClass,
        out bool allowsBreakAfter)
    {
        allowsBreakAfter = false;
        if (currentClass != LineBreakClass.ComplexContext && nextClass != LineBreakClass.ComplexContext)
        {
            return false;
        }

        if (_dictionaryBreakProvider is not null &&
            _dictionaryBreakProvider.TryResolveComplexContextBreak(codepoints, index, out allowsBreakAfter))
        {
            return true;
        }

        allowsBreakAfter = false;
        return true;
    }

    private static bool IsAlphabeticNoBreak(LineBreakClass currentClass, LineBreakClass nextClass)
    {
        return IsAlphabeticLike(currentClass) &&
               IsAlphabeticLike(nextClass);
    }

    private static bool IsAlphabeticLike(LineBreakClass lineBreakClass)
    {
        return lineBreakClass is LineBreakClass.Alphabetic or
                                 LineBreakClass.Numeric or
                                 LineBreakClass.PrefixNumeric or
                                 LineBreakClass.PostfixNumeric;
    }

    private static bool IsCjkBreakClass(LineBreakClass lineBreakClass)
    {
        return lineBreakClass is LineBreakClass.Ideographic or LineBreakClass.Nonstarter;
    }

    private static bool IsKeepAllBreakClass(LineBreakClass lineBreakClass)
    {
        return IsCjkBreakClass(lineBreakClass) ||
               lineBreakClass is LineBreakClass.HangulL or
                                 LineBreakClass.HangulV or
                                 LineBreakClass.HangulT or
                                 LineBreakClass.HangulLv or
                                 LineBreakClass.HangulLvt;
    }

    private static bool IsWordInitialHyphenBreak(
        int previousScalar,
        LineBreakClass currentClass,
        LineBreakClass nextClass)
    {
        if (currentClass != LineBreakClass.Hyphen ||
            !IsAlphabeticLike(nextClass))
        {
            return false;
        }

        return previousScalar < 0 || IsWhitespaceScalar(previousScalar) || IsOpeningPunctuation(previousScalar);
    }

    private static bool IsNumericExpressionNoBreak(
        int previousScalar,
        int currentScalar,
        int nextScalar,
        LineBreakClass currentClass,
        LineBreakClass nextClass)
    {
        if (previousScalar >= 0 &&
            IsNumericScalar(previousScalar) &&
            IsNumericJoinerClass(currentClass) &&
            IsNumericScalar(nextScalar))
        {
            return true;
        }

        if (currentClass == LineBreakClass.Numeric &&
            IsNumericJoinerClass(nextClass))
        {
            return true;
        }

        return currentScalar == 0x002F &&
               previousScalar >= 0 &&
               IsNumericScalar(previousScalar) &&
               IsNumericScalar(nextScalar);
    }

    private static bool IsApostropheNoBreak(
        int previousScalar,
        int currentScalar,
        int nextScalar,
        LineBreakClass currentClass,
        LineBreakClass nextClass)
    {
        _ = nextScalar;
        if (!IsApostropheScalar(currentScalar) ||
            !IsAlphabeticLike(nextClass) ||
            previousScalar < 0 ||
            !IsAlphabeticScalar(previousScalar))
        {
            return false;
        }

        return currentClass is LineBreakClass.BreakAfter or LineBreakClass.Other;
    }

    private static bool IsNumericJoinerClass(LineBreakClass lineBreakClass)
    {
        return lineBreakClass is LineBreakClass.InfixNumeric or
                                 LineBreakClass.SymbolBreakAfter or
                                 LineBreakClass.Hyphen;
    }

    private static bool IsNumericScalar(int scalar)
    {
        return scalar >= 0 &&
               CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(scalar), 0) == UnicodeCategory.DecimalDigitNumber;
    }

    private static bool IsAlphabeticScalar(int scalar)
    {
        if (scalar < 0)
        {
            return false;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(scalar), 0);
        return category is UnicodeCategory.LowercaseLetter or
                           UnicodeCategory.UppercaseLetter or
                           UnicodeCategory.TitlecaseLetter or
                           UnicodeCategory.ModifierLetter or
                           UnicodeCategory.OtherLetter or
                           UnicodeCategory.LetterNumber;
    }

    private static bool IsApostropheScalar(int scalar)
    {
        return scalar is 0x0027 or 0x2019;
    }

    private static bool IsWhitespaceScalar(int scalar)
    {
        return scalar >= 0 && char.IsWhiteSpace(char.ConvertFromUtf32(scalar), 0);
    }

    private static bool IsWhitespaceCodepoint(string codepoint)
    {
        return !string.IsNullOrEmpty(codepoint) && char.IsWhiteSpace(codepoint, 0);
    }

    private static bool IsOpeningPunctuation(int scalar)
    {
        return scalar is 0x0028 or 0x005B or 0x007B or
            0x2018 or 0x201C or
            0x3008 or 0x300A or 0x300C or 0x300E or 0x3010 or 0x3014 or 0x3016 or 0x3018 or 0x301A or
            0xFF08 or 0xFF3B or 0xFF5B;
    }

    private static bool IsNonEastAsianClosingToAlnumNoBreak(LineBreakClass currentClass, LineBreakClass nextClass)
    {
        return currentClass == LineBreakClass.ClosingParenthesis &&
               IsAlphabeticLike(nextClass);
    }

    private static bool IsAlnumToNonEastAsianOpeningNoBreak(LineBreakClass currentClass, LineBreakClass nextClass)
    {
        return IsAlphabeticLike(currentClass) &&
               nextClass == LineBreakClass.Opening;
    }

    private static bool IsHangulSyllableNoBreak(LineBreakClass currentClass, LineBreakClass nextClass)
    {
        return currentClass == LineBreakClass.HangulL &&
               nextClass is LineBreakClass.HangulL or LineBreakClass.HangulV or LineBreakClass.HangulLv or LineBreakClass.HangulLvt ||
               currentClass is LineBreakClass.HangulV or LineBreakClass.HangulLv &&
               nextClass is LineBreakClass.HangulV or LineBreakClass.HangulT ||
               currentClass is LineBreakClass.HangulT or LineBreakClass.HangulLvt &&
               nextClass == LineBreakClass.HangulT;
    }

    private static bool IsRegionalIndicatorPairNoBreak(
        IReadOnlyList<string> codepoints,
        int index,
        LineBreakClass currentClass,
        LineBreakClass nextClass)
    {
        if (currentClass != LineBreakClass.RegionalIndicator ||
            nextClass != LineBreakClass.RegionalIndicator)
        {
            return false;
        }

        var runLength = 0;
        for (var i = index; i >= 0; i--)
        {
            var scalar = GetCodepointScalar(codepoints[i]);
            if (!IsRegionalIndicator(scalar))
            {
                break;
            }

            runLength++;
        }

        return runLength % 2 == 1;
    }

    private static bool IsComplexContextCodepoint(int scalar)
    {
        return scalar is >= 0x0E00 and <= 0x0E7F or
                         >= 0x0E80 and <= 0x0EFF or
                         >= 0x1000 and <= 0x109F or
                         >= 0x1780 and <= 0x17FF;
    }

    private static bool IsPrependCodepoint(int scalar)
    {
        return scalar is 0x0600 or 0x0601 or 0x0602 or 0x0603 or 0x0604 or 0x0605 or
                         0x06DD or 0x070F or 0x0890 or 0x0891 or
                         >= 0x110BD and <= 0x110CD;
    }

    private static bool IsHangulGraphemeClusterContinuation(int previousScalar, int currentScalar)
    {
        return IsHangulJamoL(previousScalar) &&
               (IsHangulJamoL(currentScalar) || IsHangulJamoV(currentScalar) || IsHangulLvSyllable(currentScalar) || IsHangulLvtSyllable(currentScalar)) ||
               (IsHangulJamoV(previousScalar) || IsHangulLvSyllable(previousScalar)) &&
               (IsHangulJamoV(currentScalar) || IsHangulJamoT(currentScalar)) ||
               (IsHangulJamoT(previousScalar) || IsHangulLvtSyllable(previousScalar)) &&
               IsHangulJamoT(currentScalar);
    }

    private static bool IsHangulJamoL(int scalar)
    {
        return scalar is >= 0x1100 and <= 0x115F or >= 0xA960 and <= 0xA97C;
    }

    private static bool IsHangulJamoV(int scalar)
    {
        return scalar is >= 0x1160 and <= 0x11A7 or >= 0xD7B0 and <= 0xD7C6;
    }

    private static bool IsHangulJamoT(int scalar)
    {
        return scalar is >= 0x11A8 and <= 0x11FF or >= 0xD7CB and <= 0xD7FB;
    }

    private static bool IsHangulLvSyllable(int scalar)
    {
        return scalar is >= 0xAC00 and <= 0xD7A3 &&
               (scalar - 0xAC00) % 28 == 0;
    }

    private static bool IsHangulLvtSyllable(int scalar)
    {
        return scalar is >= 0xAC00 and <= 0xD7A3 &&
               (scalar - 0xAC00) % 28 != 0;
    }

    private static bool IsCjkBreakCodepoint(int scalar)
    {
        return scalar switch
        {
            >= 0x3040 and <= 0x30FF => true,
            >= 0x31F0 and <= 0x31FF => true,
            >= 0x3400 and <= 0x4DBF => true,
            >= 0x4E00 and <= 0x9FFF => true,
            >= 0xAC00 and <= 0xD7AF => true,
            >= 0xF900 and <= 0xFAFF => true,
            >= 0x20000 and <= 0x2A6DF => true,
            >= 0x2A700 and <= 0x2B73F => true,
            >= 0x2B740 and <= 0x2B81F => true,
            >= 0x2B820 and <= 0x2CEAF => true,
            >= 0x2CEB0 and <= 0x2EBEF => true,
            >= 0x30000 and <= 0x3134F => true,
            _ => false
        };
    }

    private static bool IsDashBreakCodepoint(int scalar)
    {
        return scalar is 0x002D or 0x058A or 0x05BE or 0x1400 or 0x1806 or 0x2010 or 0x2012 or 0x2013 or 0x2014 or 0x2E17 or 0x30A0 or 0xFE31 or 0xFE32 or 0xFE58 or 0xFE63 or 0xFF0D or 0x10EAD;
    }

    private static bool IsStrictLineBreakProhibitedBefore(int scalar)
    {
        return scalar switch
        {
            0x3041 or 0x3043 or 0x3045 or 0x3047 or 0x3049 or 0x3063 or 0x3083 or 0x3085 or 0x3087 or 0x308E => true,
            0x3095 or 0x3096 => true,
            0x30A1 or 0x30A3 or 0x30A5 or 0x30A7 or 0x30A9 or 0x30C3 or 0x30E3 or 0x30E5 or 0x30E7 or 0x30EE => true,
            0x30F5 or 0x30F6 => true,
            0x31F0 or 0x31F1 or 0x31F2 or 0x31F3 or 0x31F4 or 0x31F5 or 0x31F6 or 0x31F7 or 0x31F8 or 0x31F9 or 0x31FA or 0x31FB or 0x31FC or 0x31FD or 0x31FE or 0x31FF => true,
            0xFF67 or 0xFF68 or 0xFF69 or 0xFF6A or 0xFF6B or 0xFF6C or 0xFF6D or 0xFF6E or 0xFF6F => true,
            _ => false
        };
    }

    private static bool IsRegionalIndicator(int scalar)
    {
        return scalar is >= 0x1F1E6 and <= 0x1F1FF;
    }

    private bool IsClusterBoundaryAroundInvisibleBreak(IReadOnlyList<string> codepoints, int index)
    {
        if (index > 0 && IsCodepointClusterExtender(codepoints[index - 1]))
        {
            return false;
        }

        return index + 1 >= codepoints.Count || !IsCodepointClusterExtender(codepoints[index + 1]);
    }

    private bool IsCodepointClusterExtender(string codepoint)
    {
        if (string.IsNullOrEmpty(codepoint))
        {
            return false;
        }

        return IsGraphemeClusterExtender(char.ConvertToUtf32(codepoint, 0));
    }
}

internal static class SvgDefaultTextBoundaryResolver
{
    public static SvgTextBoundaryResolver Instance => SvgTextBoundaryResolver.Default;
}
