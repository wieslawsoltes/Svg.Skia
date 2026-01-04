// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using Svg.Skia.TypefaceProviders;

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
        return new ShimSkiaSharp.SKImage { Data = data, Width = image.Width, Height = image.Height };
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
        var typefaceCache = new Dictionary<int, SkiaSharp.SKTypeface?>();
        var fontManager = SkiaSharp.SKFontManager.Default;
        var providers = _skiaModel.Settings.TypefaceProviders;
        var hasProviders = providers is { Count: > 0 };

        if (paintPreferredTypeface.Typeface is { } preferredTypeface)
        {
            var weight = _skiaModel.ToSKFontStyleWeight(preferredTypeface.FontWeight);
            var width = _skiaModel.ToSKFontStyleWidth(preferredTypeface.FontWidth);
            var slant = _skiaModel.ToSKFontStyleSlant(preferredTypeface.FontSlant);
            var providerTypefaces = hasProviders
                ? GetProviderTypefaces(providers, preferredTypeface.FamilyName, weight, width, slant)
                : null;

            matchCharacter = codepoint =>
            {
                if (typefaceCache.TryGetValue(codepoint, out var cached))
                {
                    return cached;
                }

                SkiaSharp.SKTypeface? matched = null;

                // First try to find a matching typeface from custom providers
                matched = TryMatchCharacterFromCustomProviders(providerTypefaces, codepoint);

                // Fall back to default font manager
                matched ??= fontManager.MatchCharacter(
                    preferredTypeface.FamilyName,
                    weight,
                    width,
                    slant,
                    null,
                    codepoint);

                typefaceCache[codepoint] = matched;
                return matched;
            };
        }
        else
        {
            var providerTypefaces = hasProviders
                ? GetProviderTypefaces(providers, null, SkiaSharp.SKFontStyleWeight.Normal, SkiaSharp.SKFontStyleWidth.Normal, SkiaSharp.SKFontStyleSlant.Upright)
                : null;

            matchCharacter = codepoint =>
            {
                if (typefaceCache.TryGetValue(codepoint, out var cached))
                {
                    return cached;
                }

                SkiaSharp.SKTypeface? matched = null;

                // First try to find a matching typeface from custom providers
                matched = TryMatchCharacterFromCustomProviders(providerTypefaces, codepoint);

                // Fall back to default font manager
                matched ??= fontManager.MatchCharacter(codepoint);

                typefaceCache[codepoint] = matched;
                return matched;
            };
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
            var segmentLength = i - currentTypefaceStartIndex;
            if (segmentLength <= 0)
            {
                return;
            }

            var currentTypefaceText = currentTypefaceStartIndex == 0 && i == text.Length
                ? text
                : text.Substring(currentTypefaceStartIndex, segmentLength);

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

    /// <summary>
    /// Resolves typefaces from custom providers for the requested family and style.
    /// </summary>
    /// <param name="familyName">The preferred font family name.</param>
    /// <param name="weight">The font weight.</param>
    /// <param name="width">The font width.</param>
    /// <param name="slant">The font slant.</param>
    /// <returns>Resolved provider typefaces, or null when none are available.</returns>
    private static List<SkiaSharp.SKTypeface>? GetProviderTypefaces(IList<ITypefaceProvider>? providers, string? familyName, SkiaSharp.SKFontStyleWeight weight, SkiaSharp.SKFontStyleWidth width, SkiaSharp.SKFontStyleSlant slant)
    {
        if (providers is null || providers.Count == 0)
        {
            return null;
        }

        var resolvedFamilyName = familyName ?? "Default";
        var typefaces = new List<SkiaSharp.SKTypeface>(providers.Count);

        foreach (var provider in providers)
        {
            var typeface = provider.FromFamilyName(resolvedFamilyName, weight, width, slant);
            if (typeface is { } && !typefaces.Contains(typeface))
            {
                typefaces.Add(typeface);
            }
        }

        return typefaces;
    }

    private static SkiaSharp.SKTypeface? TryMatchCharacterFromCustomProviders(IReadOnlyList<SkiaSharp.SKTypeface>? typefaces, int codepoint)
    {
        if (typefaces is null || typefaces.Count == 0)
        {
            return null;
        }

        foreach (var typeface in typefaces)
        {
            if (typeface.ContainsGlyph(codepoint))
            {
                return typeface;
            }
        }

        return null;
    }
}
