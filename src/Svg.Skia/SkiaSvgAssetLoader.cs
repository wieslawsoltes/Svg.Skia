// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;

namespace Svg.Skia;

/// <summary>
/// Asset loader implementation using SkiaSharp types.
/// </summary>
public class SkiaSvgAssetLoader : Model.ISvgAssetLoader
{
    private readonly SkiaModel _skiaModel;

    /// <summary>
    /// Initializes a new instance of <see cref="SkiaSvgAssetLoader"/>.
    /// </summary>
    /// <param name="skiaModel">Model used to convert font data.</param>
    public SkiaSvgAssetLoader(SkiaModel skiaModel)
    {
        _skiaModel = skiaModel;
    }

    /// <inheritdoc />
    public ShimSkiaSharp.SKImage LoadImage(System.IO.Stream stream)
    {
        var data = ShimSkiaSharp.SKImage.FromStream(stream);
        using var image = SkiaSharp.SKImage.FromEncodedData(data);
        return new ShimSkiaSharp.SKImage {Data = data, Width = image.Width, Height = image.Height};
    }

    /// <inheritdoc />
    public List<Model.TypefaceSpan> FindTypefaces(string? text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        var ret = new List<Model.TypefaceSpan>();

        if (text is null || string.IsNullOrEmpty(text))
        {
            return ret;
        }

        System.Func<int, SkiaSharp.SKTypeface?> matchCharacter;

        if (paintPreferredTypeface.Typeface is { } preferredTypeface)
        {
            var weight = _skiaModel.ToSKFontStyleWeight(preferredTypeface.FontWeight);
            var width = _skiaModel.ToSKFontStyleWidth(preferredTypeface.FontWidth);
            var slant = _skiaModel.ToSKFontStyleSlant(preferredTypeface.FontSlant);

            matchCharacter = codepoint => SkiaSharp.SKFontManager.Default.MatchCharacter(
                preferredTypeface.FamilyName,
                weight,
                width,
                slant,
                null,
                codepoint);
        }
        else
        {
            matchCharacter = codepoint => SkiaSharp.SKFontManager.Default.MatchCharacter(codepoint);
        }

        using var runningPaint = _skiaModel.ToSKPaint(paintPreferredTypeface);
        if (runningPaint is null)
        {
            return ret;
        }

        var currentTypefaceStartIndex = 0;
        var i = 0;

        void YieldCurrentTypefaceText()
        {
            var currentTypefaceText = text.Substring(currentTypefaceStartIndex, i - currentTypefaceStartIndex);

            ret.Add(new(currentTypefaceText, runningPaint.MeasureText(currentTypefaceText),
                runningPaint.Typeface is null
                    ? null
                    : ShimSkiaSharp.SKTypeface.FromFamilyName(
                        runningPaint.Typeface.FamilyName,
                        // SkiaSharp provides int properties here. Let's just assume our
                        // ShimSkiaSharp defines the same values as SkiaSharp and convert directly
                        (ShimSkiaSharp.SKFontStyleWeight)runningPaint.Typeface.FontWeight,
                        (ShimSkiaSharp.SKFontStyleWidth)runningPaint.Typeface.FontWidth,
                        (ShimSkiaSharp.SKFontStyleSlant)runningPaint.Typeface.FontSlant)
            ));
        }

        for (; i < text.Length; i++)
        {
            var typeface = matchCharacter(char.ConvertToUtf32(text, i));

            if (i == 0)
            {
                runningPaint.Typeface = typeface;
            }
            else if (runningPaint.Typeface is null
                     && typeface is { } || runningPaint.Typeface is { }
                     && typeface is null || runningPaint.Typeface is { } l
                     && typeface is { } r
                     && (l.FamilyName, l.FontWeight, l.FontWidth, l.FontSlant) != (r.FamilyName, r.FontWeight, r.FontWidth, r.FontSlant))
            {
                YieldCurrentTypefaceText();

                currentTypefaceStartIndex = i;
                runningPaint.Typeface = typeface;
            }

            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }

        YieldCurrentTypefaceText();

        return ret;
    }

    /// <inheritdoc />
    public ShimSkiaSharp.SKFontMetrics GetFontMetrics(ShimSkiaSharp.SKPaint paint)
    {
        using var skPaint = _skiaModel.ToSKPaint(paint);
        if (skPaint is null)
        {
            return default;
        }

        skPaint.GetFontMetrics(out var skMetrics);
        return new ShimSkiaSharp.SKFontMetrics
        {
            Top = skMetrics.Top,
            Ascent = skMetrics.Ascent,
            Descent = skMetrics.Descent,
            Bottom = skMetrics.Bottom,
            Leading = skMetrics.Leading
        };
    }

    /// <inheritdoc />
    public float MeasureText(string? text, ShimSkiaSharp.SKPaint paint, ref ShimSkiaSharp.SKRect bounds)
    {
        using var skPaint = _skiaModel.ToSKPaint(paint);
        if (skPaint is null || text is null)
        {
            bounds = default;
            return 0f;
        }

        var skBounds = new SkiaSharp.SKRect();
        var width = skPaint.MeasureText(text, ref skBounds);
        bounds = new ShimSkiaSharp.SKRect(skBounds.Left, skBounds.Top, skBounds.Right, skBounds.Bottom);
        return width;
    }

    /// <inheritdoc />
    public ShimSkiaSharp.SKPath? GetTextPath(string? text, ShimSkiaSharp.SKPaint paint, float x, float y)
    {
        using var skPaint = _skiaModel.ToSKPaint(paint);
        if (skPaint is null || text is null)
        {
            return null;
        }

        using var skPath = skPaint.GetTextPath(text, x, y);
        return _skiaModel.FromSKPath(skPath);
    }
}
