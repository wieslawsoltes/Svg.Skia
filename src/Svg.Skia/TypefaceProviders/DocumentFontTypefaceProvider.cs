// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Svg.Skia.TypefaceProviders;

internal sealed class DocumentFontTypefaceProvider : ITypefaceProvider
{
    private sealed class Entry
    {
        public Entry(
            string familyName,
            SkiaSharp.SKFontStyleWeight weight,
            SkiaSharp.SKFontStyleWidth width,
            SkiaSharp.SKFontStyleSlant slant,
            SkiaSharp.SKTypeface typeface)
        {
            FamilyName = familyName;
            Weight = weight;
            Width = width;
            Slant = slant;
            Typeface = typeface;
        }

        public string FamilyName { get; }

        public SkiaSharp.SKFontStyleWeight Weight { get; }

        public SkiaSharp.SKFontStyleWidth Width { get; }

        public SkiaSharp.SKFontStyleSlant Slant { get; }

        public SkiaSharp.SKTypeface Typeface { get; }
    }

    private static readonly char[] s_fontFamilyTrim = { '\'', '"' };
    private readonly List<Entry> _entries = new();

    public bool IsEmpty => _entries.Count == 0;

    public void Add(
        string familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        SkiaSharp.SKTypeface typeface)
    {
        if (string.IsNullOrWhiteSpace(familyName) ||
            typeface.Handle == IntPtr.Zero)
        {
            return;
        }

        _entries.Add(new Entry(
            familyName.Trim().Trim(s_fontFamilyTrim),
            weight,
            width,
            slant,
            typeface));
    }

    public SkiaSharp.SKTypeface? FromFamilyName(
        string fontFamily,
        SkiaSharp.SKFontStyleWeight fontWeight,
        SkiaSharp.SKFontStyleWidth fontWidth,
        SkiaSharp.SKFontStyleSlant fontStyle)
    {
        if (_entries.Count == 0 || string.IsNullOrWhiteSpace(fontFamily))
        {
            return null;
        }

        var requestedFamilies = fontFamily
            .Split(',')
            .Select(static family => family.Trim().Trim(s_fontFamilyTrim))
            .Where(static family => family.Length > 0);

        foreach (var requestedFamily in requestedFamilies)
        {
            Entry? best = null;
            var bestScore = int.MaxValue;
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!string.Equals(entry.FamilyName, requestedFamily, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var score =
                    Math.Abs((int)entry.Weight - (int)fontWeight) +
                    (entry.Width == fontWidth ? 0 : Math.Abs((int)entry.Width - (int)fontWidth) * 10) +
                    (entry.Slant == fontStyle ? 0 : 1000);
                if (score < bestScore)
                {
                    best = entry;
                    bestScore = score;
                }
            }

            if (best is { })
            {
                return best.Typeface;
            }
        }

        return null;
    }
}
