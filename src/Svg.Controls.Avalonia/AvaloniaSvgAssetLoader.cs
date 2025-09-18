// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using ShimSkiaSharp;
using AMI = Avalonia.Media.Imaging;
using SM = Svg.Model;

namespace Avalonia.Svg;

/// <summary>
/// Asset loader implementation using Avalonia types.
/// </summary>
public class AvaloniaSvgAssetLoader : SM.ISvgAssetLoader
{
    /// <inheritdoc />
    public SKImage LoadImage(Stream stream)
    {
        var data = SKImage.FromStream(stream);
        using var image = new AMI.Bitmap(stream);
        return new SKImage { Data = data, Width = (float)image.Size.Width, Height = (float)image.Size.Height };
    }

    /// <inheritdoc />
    public List<SM.TypefaceSpan> FindTypefaces(string? text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        var ret = new List<SM.TypefaceSpan>();

        if (text is null || string.IsNullOrEmpty(text))
        {
            return ret;
        }

        System.Func<int, Typeface?> matchCharacter;

        if (paintPreferredTypeface.Typeface is { } preferredTypeface)
        {
            var weight = preferredTypeface.FontWeight.ToFontWeight();
            var width = preferredTypeface.FontWidth.ToFontStretch();
            var slant = preferredTypeface.FontSlant.ToFontStyle();

            matchCharacter = codepoint =>
            {
                FontManager.Current.TryMatchCharacter(
                    codepoint,
                    slant,
                    weight,
                    width,
                    preferredTypeface.FamilyName is { } n
                        ? FontFamily.Parse(n)
                        : null,
                    null, out var typeface);

                return typeface;
            };
        }
        else
        {
            matchCharacter = codepoint =>
            {
                FontManager.Current.TryMatchCharacter(
                    codepoint,
                    FontStyle.Normal,
                    FontWeight.Normal,
                    FontStretch.Normal,
                    null,
                    null,
                    out var typeface
                );

                return typeface;
            };
        }

        var runningTypeface = paintPreferredTypeface.Typeface.ToTypeface();
        var runningAdvance = 0f;
        var currentTypefaceStartIndex = 0;
        var i = 0;

        void YieldCurrentTypefaceText()
        {
            var currentTypefaceText = text.Substring(currentTypefaceStartIndex, i - currentTypefaceStartIndex);

            ret.Add(
                new(
                    currentTypefaceText,
                    runningAdvance * paintPreferredTypeface.TextSize,
                    runningTypeface is not { } typeface
                        ? null
                        : ShimSkiaSharp.SKTypeface.FromFamilyName(
                            typeface.FontFamily.Name,
                            typeface.Weight.ToSKFontWeight(),
                            typeface.Stretch.ToSKFontStretch(),
                            typeface.Style.ToSKFontStyle()
                        )));
        }

        for (; i < text.Length; i++)
        {
            var codepoint = char.ConvertToUtf32(text, i);
            var typeface = matchCharacter(codepoint);
            if (i == 0)
            {
                runningTypeface = typeface;
            }
            else if (runningTypeface is null && typeface is { }
                     || runningTypeface is { } && typeface is null
                     || runningTypeface != typeface)
            {
                YieldCurrentTypefaceText();
                runningAdvance = 0;
                currentTypefaceStartIndex = i;
                runningTypeface = typeface;
            }

            var glyphTypeface = (typeface ?? Typeface.Default).GlyphTypeface;
            runningAdvance += glyphTypeface.GetGlyphAdvance(glyphTypeface.GetGlyph((uint)codepoint));

            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }

        YieldCurrentTypefaceText();

        return ret;
    }

    /// <inheritdoc />
    public SKFontMetrics GetFontMetrics(SKPaint paint)
    {
        var typeface = paint.Typeface.ToTypeface() ?? Typeface.Default;
        var metrics = typeface.GlyphTypeface.Metrics;

        return new SKFontMetrics
        {
            Top = -(float)(metrics.Ascent * paint.TextSize),
            Ascent = -(float)(metrics.Ascent * paint.TextSize),
            Descent = (float)(metrics.Descent * paint.TextSize),
            Bottom = (float)(metrics.Descent * paint.TextSize),
            Leading = (float)(metrics.LineGap * paint.TextSize)
        };
    }

    /// <inheritdoc />
    public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
    {
        if (string.IsNullOrEmpty(text))
        {
            bounds = default;
            return 0f;
        }

        var typeface = paint.Typeface.ToTypeface() ?? Typeface.Default;
        var glyphTypeface = typeface.GlyphTypeface;
        float advance = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            var codepoint = char.ConvertToUtf32(text, i);
            advance += glyphTypeface.GetGlyphAdvance(glyphTypeface.GetGlyph((uint)codepoint));
            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }

        var width = advance * paint.TextSize;
        var metrics = glyphTypeface.Metrics;
        var ascent = (float)(metrics.Ascent * paint.TextSize);
        var descent = (float)(metrics.Descent * paint.TextSize);
        bounds = new SKRect(0, -ascent, width, descent);
        return width;
    }

    /// <inheritdoc />
    public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y)
    {
        return null;
    }
}
