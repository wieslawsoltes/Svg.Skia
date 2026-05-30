// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using ShimSkiaSharp;

namespace Svg.Skia;

internal static class SvgTextBaselineResolver
{
    public static SvgDominantBaseline ResolveScriptBaseline(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SvgDominantBaseline.Alphabetic;
        }

        var charIndex = 0;
        while (TryReadNextCodepoint(text!, ref charIndex, out var scalar))
        {
            var baseline = ResolveScriptBaseline(scalar);
            if (baseline != SvgDominantBaseline.Alphabetic)
            {
                return baseline;
            }
        }

        return SvgDominantBaseline.Alphabetic;
    }

    public static SvgDominantBaseline ResolveScriptBaseline(int scalar)
    {
        if (IsCjkBaselineScript(scalar))
        {
            return SvgDominantBaseline.Ideographic;
        }

        if (IsMathematicalBaselineScript(scalar))
        {
            return SvgDominantBaseline.Mathematical;
        }

        if (IsHangingBaselineScript(scalar))
        {
            return SvgDominantBaseline.Hanging;
        }

        return SvgDominantBaseline.Alphabetic;
    }

    public static float GetNativeBaselineLineOffset(SKFontMetrics metrics, SvgDominantBaseline baseline)
    {
        return baseline switch
        {
            SvgDominantBaseline.Ideographic => metrics.Descent,
            SvgDominantBaseline.Hanging => metrics.Ascent * 0.8f,
            SvgDominantBaseline.Mathematical or SvgDominantBaseline.Middle => (metrics.Ascent + metrics.Descent) * 0.5f,
            SvgDominantBaseline.Central => (metrics.Top + metrics.Bottom) * 0.5f,
            SvgDominantBaseline.TextAfterEdge or SvgDominantBaseline.TextBottom => metrics.Bottom,
            SvgDominantBaseline.TextBeforeEdge or SvgDominantBaseline.TextTop => metrics.Top,
            _ => 0f
        };
    }

    public static bool TryReadNextCodepoint(string text, ref int charIndex, out int scalar)
    {
        while (charIndex < text.Length)
        {
            var current = text[charIndex++];
            if (char.IsWhiteSpace(current))
            {
                continue;
            }

            if (char.IsHighSurrogate(current) &&
                charIndex < text.Length &&
                char.IsLowSurrogate(text[charIndex]))
            {
                scalar = char.ConvertToUtf32(current, text[charIndex]);
                charIndex++;
                return true;
            }

            scalar = current;
            return true;
        }

        scalar = 0;
        return false;
    }

    public static bool IsCjkBaselineScript(int scalar)
    {
        return scalar is >= 0x2E80 and <= 0xA4CF or
               >= 0xAC00 and <= 0xD7AF or
               >= 0xF900 and <= 0xFAFF or
               >= 0xFE30 and <= 0xFE4F or
               >= 0x20000 and <= 0x2FA1F;
    }

    public static bool IsMathematicalBaselineScript(int scalar)
    {
        return scalar is >= 0x2200 and <= 0x22FF or
               >= 0x27C0 and <= 0x27EF or
               >= 0x2980 and <= 0x2AFF or
               >= 0x1D400 and <= 0x1D7FF;
    }

    public static bool IsHangingBaselineScript(int scalar)
    {
        return scalar is >= 0x0900 and <= 0x0D7F or
               >= 0x0F00 and <= 0x0FFF or
               >= 0x1000 and <= 0x109F;
    }
}
