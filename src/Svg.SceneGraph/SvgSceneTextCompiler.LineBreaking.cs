using System;
using System.Collections.Generic;
using System.Globalization;

namespace Svg.Skia;

internal static partial class SvgSceneTextCompiler
{
    private enum InlineSizeLineBreakClass
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

    private static bool AllowsInlineSizeSoftWrapOpportunity(
        IReadOnlyList<string> codepoints,
        int index,
        InlineSizeLineBreakOptions options)
    {
        return SvgTextLineBreakPlanner.AllowsSoftWrapOpportunity(
            codepoints,
            index,
            new SvgTextLineBreakOptions(
                options.OverflowWrapAnywhere,
                options.WordBreakBreakAll,
                options.WordBreakKeepAll,
                options.LineBreakAnywhere,
                options.LineBreakLoose,
                options.StrictLineBreak));
    }

    private static int GetInlineSizeCodepointScalar(string codepoint)
    {
        return string.IsNullOrEmpty(codepoint) ? -1 : char.ConvertToUtf32(codepoint, 0);
    }

    private static bool IsInlineSizeTypographicBreakBoundary(int currentScalar, int nextScalar)
    {
        return !IsCombiningOrJoiningCodepoint(currentScalar) &&
               !IsCombiningOrJoiningCodepoint(nextScalar) &&
               currentScalar != 0x200D &&
               nextScalar != 0x200D;
    }

    private static bool IsInlineSizeEmergencyBreakSuppressed(
        InlineSizeLineBreakClass currentClass,
        InlineSizeLineBreakClass nextClass)
    {
        return currentClass is InlineSizeLineBreakClass.Combining or
                               InlineSizeLineBreakClass.ZeroWidthJoiner ||
               nextClass is InlineSizeLineBreakClass.Combining or
                            InlineSizeLineBreakClass.ZeroWidthJoiner;
    }

    private static InlineSizeLineBreakClass GetInlineSizeLineBreakClass(int scalar)
    {
        if (IsCombiningOrJoiningCodepoint(scalar))
        {
            return scalar == 0x200D
                ? InlineSizeLineBreakClass.ZeroWidthJoiner
                : InlineSizeLineBreakClass.Combining;
        }

        if (IsCjkInlineBreakCodepoint(scalar) ||
            scalar is >= 0x3000 and <= 0x303F or >= 0xFF00 and <= 0xFFEF)
        {
            return ClassifyInlineSizeCjkLineBreakClass(scalar);
        }

        if (IsInlineSizeComplexContextCodepoint(scalar))
        {
            return InlineSizeLineBreakClass.ComplexContext;
        }

        if (IsInlineSizeHangulJamoL(scalar))
        {
            return InlineSizeLineBreakClass.HangulL;
        }

        if (IsInlineSizeHangulJamoV(scalar))
        {
            return InlineSizeLineBreakClass.HangulV;
        }

        if (IsInlineSizeHangulJamoT(scalar))
        {
            return InlineSizeLineBreakClass.HangulT;
        }

        if (IsInlineSizeHangulLvSyllable(scalar))
        {
            return InlineSizeLineBreakClass.HangulLv;
        }

        if (IsInlineSizeHangulLvtSyllable(scalar))
        {
            return InlineSizeLineBreakClass.HangulLvt;
        }

        if (scalar is >= 0x1F1E6 and <= 0x1F1FF)
        {
            return InlineSizeLineBreakClass.RegionalIndicator;
        }

        if (scalar is 0x00A0 or 0x2007 or 0x202F)
        {
            return InlineSizeLineBreakClass.Glue;
        }

        if (scalar is 0x2060 or 0xFEFF)
        {
            return InlineSizeLineBreakClass.WordJoiner;
        }

        if (IsDashInlineBreakCodepoint(scalar))
        {
            return InlineSizeLineBreakClass.Hyphen;
        }

        if (scalar is 0x002F)
        {
            return InlineSizeLineBreakClass.SymbolBreakAfter;
        }

        if (scalar is 0x002C or 0x002E or 0x003A or 0x003B)
        {
            return InlineSizeLineBreakClass.InfixNumeric;
        }

        if (scalar is 0x0021 or 0x003F or 0x203C or 0x2047 or 0x2048 or 0x2049)
        {
            return InlineSizeLineBreakClass.Exclamation;
        }

        if (scalar is 0x0028 or 0x005B or 0x007B)
        {
            return InlineSizeLineBreakClass.Opening;
        }

        if (scalar is 0x0029 or 0x005D or 0x007D)
        {
            return InlineSizeLineBreakClass.ClosingParenthesis;
        }

        if (scalar is 0x0025 or 0x00A2 or 0x00B0 or 0x2030 or 0x2031)
        {
            return InlineSizeLineBreakClass.PostfixNumeric;
        }

        if (scalar is 0x0024 or 0x00A3 or 0x00A5 or 0x20AC)
        {
            return InlineSizeLineBreakClass.PrefixNumeric;
        }

        if (scalar is 0x0026 or 0x002B)
        {
            return InlineSizeLineBreakClass.BreakBefore;
        }

        if (scalar is 0x007C)
        {
            return InlineSizeLineBreakClass.BreakBoth;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(scalar), 0);
        return category switch
        {
            UnicodeCategory.DecimalDigitNumber => InlineSizeLineBreakClass.Numeric,
            UnicodeCategory.OpenPunctuation or UnicodeCategory.InitialQuotePunctuation => InlineSizeLineBreakClass.Opening,
            UnicodeCategory.ClosePunctuation or UnicodeCategory.FinalQuotePunctuation => InlineSizeLineBreakClass.Closing,
            UnicodeCategory.DashPunctuation => InlineSizeLineBreakClass.Hyphen,
            UnicodeCategory.OtherPunctuation => InlineSizeLineBreakClass.BreakAfter,
            UnicodeCategory.CurrencySymbol => InlineSizeLineBreakClass.PrefixNumeric,
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.LetterNumber => InlineSizeLineBreakClass.Alphabetic,
            _ => InlineSizeLineBreakClass.Other
        };
    }

    private static InlineSizeLineBreakClass ClassifyInlineSizeCjkLineBreakClass(int scalar)
    {
        if (scalar is 0x3008 or 0x300A or 0x300C or 0x300E or 0x3010 or 0x3014 or 0x3016 or 0x3018 or 0x301A or
            0xFF08 or 0xFF3B or 0xFF5B)
        {
            return InlineSizeLineBreakClass.Opening;
        }

        if (scalar is 0x3009 or 0x300B or 0x300D or 0x300F or 0x3011 or 0x3015 or 0x3017 or 0x3019 or 0x301B or
            0x301E or 0x301F or 0xFF09 or 0xFF3D or 0xFF5D)
        {
            return InlineSizeLineBreakClass.Closing;
        }

        if (scalar is 0x3001 or 0x3002 or 0xFF0C or 0xFF0E)
        {
            return InlineSizeLineBreakClass.InfixNumeric;
        }

        if (scalar is 0xFF01 or 0xFF1F or 0xFF1A or 0xFF1B)
        {
            return InlineSizeLineBreakClass.Exclamation;
        }

        if (scalar is 0x30FB or 0xFF65)
        {
            return InlineSizeLineBreakClass.BreakAfter;
        }

        if (IsStrictLineBreakProhibitedBefore(scalar))
        {
            return InlineSizeLineBreakClass.Nonstarter;
        }

        return InlineSizeLineBreakClass.Ideographic;
    }

    private static bool IsInlineSizeBreakProhibitedBefore(
        InlineSizeLineBreakClass lineBreakClass,
        int scalar,
        InlineSizeLineBreakOptions options)
    {
        if (lineBreakClass is InlineSizeLineBreakClass.Combining or
            InlineSizeLineBreakClass.ZeroWidthJoiner or
            InlineSizeLineBreakClass.Glue or
            InlineSizeLineBreakClass.WordJoiner or
            InlineSizeLineBreakClass.Closing or
            InlineSizeLineBreakClass.ClosingParenthesis or
            InlineSizeLineBreakClass.Exclamation or
            InlineSizeLineBreakClass.InfixNumeric or
            InlineSizeLineBreakClass.SymbolBreakAfter or
            InlineSizeLineBreakClass.PostfixNumeric)
        {
            return true;
        }

        return options.StrictLineBreak &&
               (lineBreakClass == InlineSizeLineBreakClass.Nonstarter ||
                IsStrictLineBreakProhibitedBefore(scalar));
    }

    private static bool IsInlineSizeBreakProhibitedAfter(
        InlineSizeLineBreakClass lineBreakClass,
        int scalar,
        InlineSizeLineBreakOptions options)
    {
        _ = options;
        _ = scalar;
        return lineBreakClass is InlineSizeLineBreakClass.Combining or
                                  InlineSizeLineBreakClass.ZeroWidthJoiner or
                                  InlineSizeLineBreakClass.Glue or
                                  InlineSizeLineBreakClass.WordJoiner or
                                  InlineSizeLineBreakClass.Opening or
                                  InlineSizeLineBreakClass.PrefixNumeric or
                                  InlineSizeLineBreakClass.BreakBefore;
    }

    private static bool IsInlineSizeAlphabeticNoBreak(
        InlineSizeLineBreakClass currentClass,
        InlineSizeLineBreakClass nextClass)
    {
        return IsInlineSizeAlphabeticLike(currentClass) &&
               IsInlineSizeAlphabeticLike(nextClass);
    }

    private static bool IsInlineSizeAlphabeticLike(InlineSizeLineBreakClass lineBreakClass)
    {
        return lineBreakClass is InlineSizeLineBreakClass.Alphabetic or
                                 InlineSizeLineBreakClass.Numeric or
                                 InlineSizeLineBreakClass.PrefixNumeric or
                                 InlineSizeLineBreakClass.PostfixNumeric;
    }

    private static bool IsInlineSizeCjkBreakClass(InlineSizeLineBreakClass lineBreakClass)
    {
        return lineBreakClass is InlineSizeLineBreakClass.Ideographic or
                                 InlineSizeLineBreakClass.Nonstarter;
    }

    private static bool IsInlineSizeWordInitialHyphenBreak(
        int previousScalar,
        int currentScalar,
        InlineSizeLineBreakClass currentClass,
        InlineSizeLineBreakClass nextClass)
    {
        if (currentClass != InlineSizeLineBreakClass.Hyphen ||
            !IsInlineSizeAlphabeticLike(nextClass))
        {
            return false;
        }

        return previousScalar < 0 || IsWhitespaceScalar(previousScalar) || IsInlineSizeOpeningPunctuation(previousScalar);
    }

    private static bool IsInlineSizeNumericExpressionNoBreak(
        int previousScalar,
        int currentScalar,
        int nextScalar,
        InlineSizeLineBreakClass currentClass,
        InlineSizeLineBreakClass nextClass)
    {
        if (previousScalar >= 0 &&
            IsInlineSizeNumericScalar(previousScalar) &&
            IsInlineSizeNumericJoinerClass(currentClass) &&
            IsInlineSizeNumericScalar(nextScalar))
        {
            return true;
        }

        if (currentClass == InlineSizeLineBreakClass.Numeric &&
            IsInlineSizeNumericJoinerClass(nextClass))
        {
            return true;
        }

        if (currentScalar == 0x002F &&
            previousScalar >= 0 &&
            IsInlineSizeNumericScalar(previousScalar) &&
            IsInlineSizeNumericScalar(nextScalar))
        {
            return true;
        }

        return false;
    }

    private static bool IsInlineSizeNumericJoinerClass(InlineSizeLineBreakClass lineBreakClass)
    {
        return lineBreakClass is InlineSizeLineBreakClass.InfixNumeric or
                                 InlineSizeLineBreakClass.SymbolBreakAfter or
                                 InlineSizeLineBreakClass.Hyphen;
    }

    private static bool IsInlineSizeNumericScalar(int scalar)
    {
        if (scalar < 0)
        {
            return false;
        }

        return CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(scalar), 0) == UnicodeCategory.DecimalDigitNumber;
    }

    private static bool IsWhitespaceScalar(int scalar)
    {
        return scalar >= 0 && char.IsWhiteSpace(char.ConvertFromUtf32(scalar), 0);
    }

    private static bool IsInlineSizeOpeningPunctuation(int scalar)
    {
        return scalar is 0x0028 or 0x005B or 0x007B or
            0x2018 or 0x201C or
            0x3008 or 0x300A or 0x300C or 0x300E or 0x3010 or 0x3014 or 0x3016 or 0x3018 or 0x301A or
            0xFF08 or 0xFF3B or 0xFF5B;
    }

    private static bool IsInlineSizeNonEastAsianClosingToAlnumNoBreak(
        InlineSizeLineBreakClass currentClass,
        InlineSizeLineBreakClass nextClass)
    {
        return currentClass == InlineSizeLineBreakClass.ClosingParenthesis &&
               IsInlineSizeAlphabeticLike(nextClass);
    }

    private static bool IsInlineSizeAlnumToNonEastAsianOpeningNoBreak(
        InlineSizeLineBreakClass currentClass,
        InlineSizeLineBreakClass nextClass)
    {
        return IsInlineSizeAlphabeticLike(currentClass) &&
               nextClass == InlineSizeLineBreakClass.Opening;
    }

    private static bool IsInlineSizeHangulSyllableNoBreak(
        InlineSizeLineBreakClass currentClass,
        InlineSizeLineBreakClass nextClass)
    {
        return currentClass == InlineSizeLineBreakClass.HangulL &&
               nextClass is InlineSizeLineBreakClass.HangulL or InlineSizeLineBreakClass.HangulV or InlineSizeLineBreakClass.HangulLv or InlineSizeLineBreakClass.HangulLvt ||
               currentClass is InlineSizeLineBreakClass.HangulV or InlineSizeLineBreakClass.HangulLv &&
               nextClass is InlineSizeLineBreakClass.HangulV or InlineSizeLineBreakClass.HangulT ||
               currentClass is InlineSizeLineBreakClass.HangulT or InlineSizeLineBreakClass.HangulLvt &&
               nextClass == InlineSizeLineBreakClass.HangulT;
    }

    private static bool IsInlineSizeRegionalIndicatorPairNoBreak(
        IReadOnlyList<string> codepoints,
        int index,
        InlineSizeLineBreakClass currentClass,
        InlineSizeLineBreakClass nextClass)
    {
        if (currentClass != InlineSizeLineBreakClass.RegionalIndicator ||
            nextClass != InlineSizeLineBreakClass.RegionalIndicator)
        {
            return false;
        }

        var runLength = 0;
        for (var i = index; i >= 0; i--)
        {
            var scalar = GetInlineSizeCodepointScalar(codepoints[i]);
            if (scalar is < 0 or < 0x1F1E6 or > 0x1F1FF)
            {
                break;
            }

            runLength++;
        }

        return runLength % 2 == 1;
    }

    private static bool IsInlineSizeComplexContextCodepoint(int scalar)
    {
        return scalar is >= 0x0E00 and <= 0x0E7F or
                         >= 0x0E80 and <= 0x0EFF or
                         >= 0x1000 and <= 0x109F or
                         >= 0x1780 and <= 0x17FF;
    }

    private static bool IsInlineSizeHangulJamoL(int scalar)
    {
        return scalar is >= 0x1100 and <= 0x115F or >= 0xA960 and <= 0xA97C;
    }

    private static bool IsInlineSizeHangulJamoV(int scalar)
    {
        return scalar is >= 0x1160 and <= 0x11A7 or >= 0xD7B0 and <= 0xD7C6;
    }

    private static bool IsInlineSizeHangulJamoT(int scalar)
    {
        return scalar is >= 0x11A8 and <= 0x11FF or >= 0xD7CB and <= 0xD7FB;
    }

    private static bool IsInlineSizeHangulLvSyllable(int scalar)
    {
        return scalar is >= 0xAC00 and <= 0xD7A3 &&
               (scalar - 0xAC00) % 28 == 0;
    }

    private static bool IsInlineSizeHangulLvtSyllable(int scalar)
    {
        return scalar is >= 0xAC00 and <= 0xD7A3 &&
               (scalar - 0xAC00) % 28 != 0;
    }
}
