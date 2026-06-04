// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

/// <summary>
/// Asset loader implementation using SkiaSharp types.
/// </summary>
public partial class SkiaSvgAssetLoader : Model.ISvgAssetLoader, Model.ISvgImageAlphaProvider, Model.ISvgBrokenImagePlaceholderOptions, Model.ISvgTextReferenceRenderingOptions, Model.ISvgFilterBackgroundInputOptions, Model.ISvgTextRunTypefaceResolver, Model.ISvgTextGlyphRunResolver, Model.ISvgTextDirectedGlyphRunResolver, Model.ISvgTextGlyphClusterResolver, Model.ISvgTextGlyphRunPathResolver
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
    public bool EnableSvgFonts => _skiaModel.Settings.EnableSvgFonts;

    /// <inheritdoc />
    public bool EnableTextReferences => _skiaModel.Settings.EnableTextReferences;

    /// <inheritdoc />
    public bool EnableFilterBackgroundInputs => _skiaModel.Settings.EnableFilterBackgroundInputs;

    /// <inheritdoc />
    public bool EnableBrokenImagePlaceholders => _skiaModel.Settings.EnableBrokenImagePlaceholders;

    /// <inheritdoc />
    public ShimSkiaSharp.SKImage LoadImage(System.IO.Stream stream)
    {
        var data = ShimSkiaSharp.SKImage.FromStream(stream);
        using var image = data is { Length: > 0 } ? SkiaSharp.SKImage.FromEncodedData(data) : null;
        return new ShimSkiaSharp.SKImage { Data = data, Width = image?.Width ?? 0, Height = image?.Height ?? 0 };
    }

    /// <inheritdoc />
    public bool TryGetImageAlpha(ShimSkiaSharp.SKImage image, out int width, out int height, out byte[] alpha)
    {
        width = 0;
        height = 0;
        alpha = Array.Empty<byte>();
        if (image.Data is null || image.Data.Length == 0)
        {
            return false;
        }

        using var skImage = SkiaSharp.SKImage.FromEncodedData(image.Data);
        if (skImage is null)
        {
            return false;
        }

        using var bitmap = SkiaSharp.SKBitmap.FromImage(skImage);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return false;
        }

        width = bitmap.Width;
        height = bitmap.Height;
        alpha = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                alpha[(y * width) + x] = bitmap.GetPixel(x, y).Alpha;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public List<Model.TypefaceSpan> FindTypefaces(string? text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        if (text is null || string.IsNullOrEmpty(text))
        {
            return new List<Model.TypefaceSpan>();
        }

        EnsureTypefaceProviderCaches();
        var canCacheSpans = text.Length <= TypefaceSpanCacheMaxTextLength;
        var cacheKey = canCacheSpans
            ? new TypefaceSpanCacheKey(text, paintPreferredTypeface)
            : default;
        if (canCacheSpans &&
            _typefaceSpanCache.TryGetValue(cacheKey, out var cachedSpans))
        {
            return new List<Model.TypefaceSpan>(cachedSpans);
        }

        using var runningPaint = _skiaModel.ToSKTextPaint(paintPreferredTypeface);
        if (runningPaint is null)
        {
            return new List<Model.TypefaceSpan>();
        }

        var ret = new List<Model.TypefaceSpan>();

        var preferredTypeface = paintPreferredTypeface.Typeface;
        var weight = _skiaModel.ToSKFontStyleWeight(preferredTypeface?.FontWeight ?? ShimSkiaSharp.SKFontStyleWeight.Normal);
        var requestedWeight = preferredTypeface is null ? default(SkiaSharp.SKFontStyleWeight?) : weight;
        var width = _skiaModel.ToSKFontStyleWidth(preferredTypeface?.FontWidth ?? ShimSkiaSharp.SKFontStyleWidth.Normal);
        var slant = _skiaModel.ToSKFontStyleSlant(preferredTypeface?.FontSlant ?? ShimSkiaSharp.SKFontStyleSlant.Upright);
        var preferredFamily = GetExplicitFamilyName(preferredTypeface) ??
                              (preferredTypeface is null ? null : runningPaint.Typeface?.FamilyName);
        SkiaSharp.SKTypeface? MatchCharacterForSpan(int codepoint, out string? familyOverride)
        {
            return MatchCharacterForTypefaceSpan(preferredFamily, weight, width, slant, codepoint, out familyOverride);
        }

        var currentTypefaceStartIndex = 0;
        var currentShimTypeface = default(ShimSkiaSharp.SKTypeface);
        var i = 0;

        void YieldCurrentTypefaceText()
        {
            var currentTypefaceText = text.Substring(currentTypefaceStartIndex, i - currentTypefaceStartIndex);

            ret.Add(new(currentTypefaceText, _skiaModel.GetTextAdvance(
                    currentTypefaceText,
                    runningPaint,
                    paintPreferredTypeface.FontFeatureSettings,
                    paintPreferredTypeface.FontKerning,
                    paintPreferredTypeface.FontVariantLigatures),
                runningPaint.Typeface is null
                    ? null
                    : currentShimTypeface ?? ToShimTypeface(runningPaint.Typeface, requestedWeight)
            ));
        }

        for (; i < text.Length; i++)
        {
            var ch = text[i];
            SkiaSharp.SKTypeface? typeface;
            var matchedShimTypeface = currentShimTypeface;
            if (runningPaint.Typeface is { } currentTypeface &&
                (ch <= ' ' || ch is '\u0085' or '\u00A0' || ch >= '\u0300' && IsNonAsciiTypefaceSpanGlue(text, i, ch)) &&
                CanKeepGlueInCurrentTypeface(currentTypeface, text, i, ch))
            {
                // Keep marks and whitespace in the active span so bidi/shaping stays attached to
                // the surrounding script run instead of splitting on font fallback for nonspacing
                // marks, variation selectors, format controls, or spaces.
                typeface = currentTypeface;
            }
            else
            {
                typeface = MatchCharacterForSpan(GetCodepoint(text, i, ch), out var familyOverride);
                matchedShimTypeface = ToShimTypeface(typeface, requestedWeight, familyOverride);
            }

            if (i == 0)
            {
                runningPaint.Typeface = typeface;
                currentShimTypeface = matchedShimTypeface;
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
                currentShimTypeface = matchedShimTypeface;
            }

            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }

        YieldCurrentTypefaceText();

        if (canCacheSpans)
        {
            _typefaceSpanCache.TryAdd(cacheKey, ret.ToArray());
            TrimTypefaceSpanCacheIfNeeded();
        }

        return ret;
    }

    /// <inheritdoc />
    public ShimSkiaSharp.SKTypeface? FindRunTypeface(string? text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        EnsureTypefaceProviderCaches();

        var codepoints = CollectDistinctRenderableCodepoints(text!);
        if (codepoints.Count == 0)
        {
            return paintPreferredTypeface.Typeface;
        }

        using var preferredPaint = _skiaModel.ToSKTextPaint(paintPreferredTypeface);
        var preferredTypeface = paintPreferredTypeface.Typeface;
        var preferredWeight = _skiaModel.ToSKFontStyleWeight(preferredTypeface?.FontWeight ?? ShimSkiaSharp.SKFontStyleWeight.Normal);
        var requestedWeight = preferredTypeface is null ? default(SkiaSharp.SKFontStyleWeight?) : preferredWeight;
        var preferredWidth = _skiaModel.ToSKFontStyleWidth(preferredTypeface?.FontWidth ?? ShimSkiaSharp.SKFontStyleWidth.Normal);
        var preferredSlant = _skiaModel.ToSKFontStyleSlant(preferredTypeface?.FontSlant ?? ShimSkiaSharp.SKFontStyleSlant.Upright);
        var preferredFamily = GetExplicitFamilyName(preferredTypeface) ??
                              (preferredTypeface is null ? null : preferredPaint?.Typeface?.FamilyName);

        var candidates = new List<(SkiaSharp.SKTypeface Typeface, ShimSkiaSharp.SKTypeface? ReturnTypeface)>();
        void AddCandidate(SkiaSharp.SKTypeface? candidate, ShimSkiaSharp.SKTypeface? returnTypeface = null)
        {
            if (candidate is null || candidate.Handle == IntPtr.Zero)
            {
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var existing = candidates[i].Typeface;
                var existingReturn = candidates[i].ReturnTypeface;
                if ((existing.FamilyName, existing.FontWeight, existing.FontWidth, existing.FontSlant) ==
                    (candidate.FamilyName, candidate.FontWeight, candidate.FontWidth, candidate.FontSlant) &&
                    (existingReturn?.FamilyName, existingReturn?.FontWeight, existingReturn?.FontWidth, existingReturn?.FontSlant) ==
                    (returnTypeface?.FamilyName, returnTypeface?.FontWeight, returnTypeface?.FontWidth, returnTypeface?.FontSlant))
                {
                    return;
                }
            }

            candidates.Add((candidate, returnTypeface));
        }

        var spans = FindTypefaces(text, paintPreferredTypeface);
        for (var i = 0; i < spans.Count; i++)
        {
            if (spans[i].Typeface is { } spanTypeface)
            {
                var spanNativeTypeface = _skiaModel.ToSKTypeface(spanTypeface);
                AddCandidate(spanNativeTypeface, GetRunReturnTypeface(spanTypeface, spanNativeTypeface));
            }
        }

        AddCandidate(preferredPaint?.Typeface);

        for (var i = 0; i < codepoints.Count; i++)
        {
            AddCandidate(MatchCharacter(preferredFamily, preferredWeight, preferredWidth, preferredSlant, codepoints[i]));
            AddCandidate(MatchCharacter(null, preferredWeight, preferredWidth, preferredSlant, codepoints[i]));
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i].Typeface;
            if (CanRenderAllCodepoints(candidate, codepoints))
            {
                return candidates[i].ReturnTypeface ?? ToShimTypeface(candidate, requestedWeight);
            }
        }

        return null;

        ShimSkiaSharp.SKTypeface? GetRunReturnTypeface(
            ShimSkiaSharp.SKTypeface spanTypeface,
            SkiaSharp.SKTypeface? nativeTypeface)
        {
            var spanFamilyName = spanTypeface.FamilyName;
            if (nativeTypeface is null ||
                nativeTypeface.Handle == IntPtr.Zero ||
                spanFamilyName is null ||
                string.IsNullOrWhiteSpace(spanFamilyName) ||
                spanFamilyName.IndexOf(',') < 0)
            {
                return spanTypeface;
            }

            foreach (var candidate in SkiaModel.EnumerateFontFamilyCandidates(spanFamilyName, browserCompatible: true))
            {
                var candidateTypeface = ShimSkiaSharp.SKTypeface.FromFamilyName(
                    candidate,
                    spanTypeface.FontWeight,
                    spanTypeface.FontWidth,
                    spanTypeface.FontSlant);
                var candidateNativeTypeface = _skiaModel.ToSKTypeface(candidateTypeface);
                if (candidateNativeTypeface is not null &&
                    candidateNativeTypeface.Handle != IntPtr.Zero &&
                    (candidateNativeTypeface.FamilyName, candidateNativeTypeface.FontWeight, candidateNativeTypeface.FontWidth, candidateNativeTypeface.FontSlant) ==
                    (nativeTypeface.FamilyName, nativeTypeface.FontWeight, nativeTypeface.FontWidth, nativeTypeface.FontSlant))
                {
                    return candidateTypeface;
                }
            }

            return spanTypeface;
        }
    }

    /// <inheritdoc />
    public ShimSkiaSharp.SKFontMetrics GetFontMetrics(ShimSkiaSharp.SKPaint paint)
    {
        using var skPaint = _skiaModel.ToSKTextPaint(paint);
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
            Leading = skMetrics.Leading,
            StrikeoutPosition = skMetrics.StrikeoutPosition,
            StrikeoutThickness = skMetrics.StrikeoutThickness,
            UnderlinePosition = skMetrics.UnderlinePosition,
            UnderlineThickness = skMetrics.UnderlineThickness
        };
    }

    /// <inheritdoc />
    public float MeasureText(string? text, ShimSkiaSharp.SKPaint paint, ref ShimSkiaSharp.SKRect bounds)
    {
        using var skPaint = _skiaModel.ToSKTextPaint(paint);
        if (skPaint is null || text is null)
        {
            bounds = default;
            return 0f;
        }

        var skBounds = new SkiaSharp.SKRect();
        skPaint.MeasureText(text, ref skBounds);
        var width = _skiaModel.GetTextAdvance(text, skPaint, paint.FontFeatureSettings, paint.FontKerning, paint.FontVariantLigatures);
        bounds = new ShimSkiaSharp.SKRect(skBounds.Left, skBounds.Top, skBounds.Right, skBounds.Bottom);
        return width;
    }

    /// <inheritdoc />
    public bool TryShapeGlyphRun(string? text, ShimSkiaSharp.SKPaint paint, out Model.ShapedGlyphRun shapedRun)
    {
        return _skiaModel.TryShapeGlyphRun(text, paint, out shapedRun);
    }

    /// <inheritdoc />
    public bool TryShapeGlyphRun(string? text, ShimSkiaSharp.SKPaint paint, bool rightToLeft, out Model.ShapedGlyphRun shapedRun)
    {
        return _skiaModel.TryShapeGlyphRun(text, paint, rightToLeft, out shapedRun);
    }

    /// <inheritdoc />
    public bool TryShapeGlyphClusters(string? text, ShimSkiaSharp.SKPaint paint, bool rightToLeft, out Model.ShapedGlyphRun shapedRun, out Model.ShapedTextCluster[] clusters)
    {
        return _skiaModel.TryShapeGlyphClusters(text, paint, rightToLeft, out shapedRun, out clusters);
    }

    /// <inheritdoc />
    public bool TryGetGlyphRunPath(Model.ShapedGlyphRun shapedRun, ShimSkiaSharp.SKPaint paint, float x, float y, out ShimSkiaSharp.SKPath path)
    {
        return _skiaModel.TryGetGlyphRunPath(shapedRun, paint, x, y, out path);
    }

    /// <inheritdoc />
    public ShimSkiaSharp.SKPath? GetTextPath(string? text, ShimSkiaSharp.SKPaint paint, float x, float y)
    {
        using var skPaint = _skiaModel.ToSKTextPaint(paint);
        if (skPaint is null || text is null)
        {
            return null;
        }

        using var skPath = skPaint.GetTextPath(text, x, y);
        return _skiaModel.FromSKPath(skPath);
    }

    private void EnsureTypefaceProviderCaches()
    {
        var providers = _skiaModel.Settings.TypefaceProviders;
        var documentProviders = _skiaModel.Settings.DocumentTypefaceProviders;
        var hash = ComputeTypefaceProviderHash(documentProviders, providers);
        if (!ReferenceEquals(providers, _providerStateList) || hash != _providerStateHash)
        {
            _providerStateList = providers;
            _providerStateHash = hash;
            _matchCharacterCache.Clear();
            _providerTypefaceCache.Clear();
            _typefaceSpanCache.Clear();
            ClearPaintCache();
        }
    }

    private void TrimTypefaceSpanCacheIfNeeded()
    {
        if (_typefaceSpanCache.Count > TypefaceSpanCacheLimit)
        {
            _typefaceSpanCache.Clear();
        }
    }

    private static int ComputeTypefaceProviderHash(params IList<ITypefaceProvider>?[] providerLists)
    {
        unchecked
        {
            var hash = 17;
            for (var listIndex = 0; listIndex < providerLists.Length; listIndex++)
            {
                var providers = providerLists[listIndex];
                if (providers is null)
                {
                    hash = (hash * 397) ^ -1;
                    continue;
                }

                hash = (hash * 397) ^ providers.Count;
                for (var i = 0; i < providers.Count; i++)
                {
                    var provider = providers[i];
                    if (provider is null)
                    {
                        continue;
                    }

                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(provider);
                    hash = (hash * 397) ^ provider.GetHashCode();
                    if (provider is CustomTypefaceProvider custom)
                    {
                        hash = (hash * 397) ^ (custom.Typeface?.Handle.GetHashCode() ?? 0);
                    }
                    else if (provider is FontManagerTypefaceProvider fontManagerProvider &&
                             fontManagerProvider.TryGetFontManagerHandle(out var handle))
                    {
                        hash = (hash * 397) ^ handle.GetHashCode();
                    }
                }
            }

            return hash;
        }
    }

    private void ClearPaintCache()
    {
        lock (_paintCacheLock)
        {
            foreach (var weak in _paintCacheRefs)
            {
                if (weak.TryGetTarget(out var paint) && paint.Handle != IntPtr.Zero)
                {
                    paint.Dispose();
                }
            }

            _paintCacheRefs.Clear();
            _paintCache = new ConditionalWeakTable<ShimSkiaSharp.SKPaint, CachedSkPaint>();
        }
    }

    private void TrimPaintCacheRefsIfNeeded()
    {
        if (_paintCacheRefs.Count <= PaintCacheRefTrimThreshold)
        {
            return;
        }

        for (var i = _paintCacheRefs.Count - 1; i >= 0; i--)
        {
            var weak = _paintCacheRefs[i];
            if (!weak.TryGetTarget(out var paint) || paint.Handle == IntPtr.Zero)
            {
                _paintCacheRefs.RemoveAt(i);
            }
        }
    }

    private SkiaSharp.SKPaint? GetCachedPaint(ShimSkiaSharp.SKPaint paint)
    {
        EnsureTypefaceProviderCaches();

        var signature = new PaintSignature(paint);
        lock (_paintCacheLock)
        {
            if (_paintCache.TryGetValue(paint, out var cached))
            {
                if (cached.Paint.Handle != IntPtr.Zero && cached.Signature.Equals(signature))
                {
                    return cached.Paint;
                }

                cached.Dispose();
                _paintCache.Remove(paint);
            }

            var skPaint = _skiaModel.ToSKPaint(paint);
            if (skPaint is null)
            {
                return null;
            }

            _paintCache.Add(paint, new CachedSkPaint(signature, skPaint));
            _paintCacheRefs.Add(new WeakReference<SkiaSharp.SKPaint>(skPaint));
            TrimPaintCacheRefsIfNeeded();
            return skPaint;
        }
    }

    private void TrimCachesIfNeeded()
    {
        if (_matchCharacterCache.Count > MatchCharacterCacheLimit)
        {
            _matchCharacterCache.Clear();
        }

        if (_providerTypefaceCache.Count > ProviderTypefaceCacheLimit)
        {
            _providerTypefaceCache.Clear();
        }
    }

    private SkiaSharp.SKTypeface? MatchCharacter(
        string? familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        int codepoint)
    {
        var normalizedFamily = familyName;
        var key = new MatchCharacterKey(normalizedFamily, weight, width, slant, codepoint);
        if (_matchCharacterCache.TryGetValue(key, out var cached))
        {
            if (cached is not null && cached.Handle != IntPtr.Zero)
            {
                return cached;
            }

            _matchCharacterCache.TryRemove(key, out _);
        }

        var typeface = TryMatchCharacterFromCustomProviders(normalizedFamily, weight, width, slant, codepoint);
        if (typeface is null)
        {
            if (!SharedTypefaceCache.TryGetMatchedCharacter(normalizedFamily, weight, width, slant, codepoint, out typeface))
            {
                typeface = MatchPlatformCharacter(normalizedFamily, weight, width, slant, codepoint);
                SharedTypefaceCache.AddMatchedCharacter(normalizedFamily, weight, width, slant, codepoint, typeface);
            }
        }

        if (typeface is { } && typeface.Handle == IntPtr.Zero)
        {
            typeface = null;
        }

        _matchCharacterCache.TryAdd(key, typeface);
        TrimCachesIfNeeded();
        return typeface;
    }

    private SkiaSharp.SKTypeface? MatchCharacterForTypefaceSpan(
        string? familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        int codepoint,
        out string? familyOverride)
    {
        familyOverride = null;
        var typeface = TryMatchCharacterFromCustomProviders(familyName, weight, width, slant, codepoint, out var matchedFamily);
        if (typeface is { })
        {
            familyOverride = matchedFamily;
            return typeface;
        }

        return MatchCharacter(familyName, weight, width, slant, codepoint);
    }

    private static string? GetExplicitFamilyName(ShimSkiaSharp.SKTypeface? typeface)
    {
        return SkiaModel.HasExplicitTypeface(typeface) ? typeface!.FamilyName : null;
    }

    private static SkiaSharp.SKTypeface? MatchPlatformCharacter(
        string? normalizedFamily,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        int codepoint)
    {
        var typeface = default(SkiaSharp.SKTypeface);

        if (normalizedFamily is not null)
        {
            foreach (var candidate in SkiaModel.EnumerateFontFamilyCandidates(normalizedFamily, browserCompatible: true))
            {
                if (IsGenericFamilyName(candidate))
                {
                    continue;
                }

                var matchedFamily = SkiaSharp.SKFontManager.Default.MatchFamily(
                    candidate,
                    new SkiaSharp.SKFontStyle(weight, width, slant));
                if (matchedFamily is null ||
                    matchedFamily.Handle == IntPtr.Zero ||
                    !string.Equals(matchedFamily.FamilyName, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    matchedFamily?.Dispose();
                    continue;
                }

                matchedFamily.Dispose();
                typeface = SkiaSharp.SKFontManager.Default.MatchCharacter(
                    candidate,
                    weight,
                    width,
                    slant,
                    null,
                    codepoint);

                if (typeface is { })
                {
                    break;
                }
            }

            if (typeface is null && ShouldUseSerifDefaultFallback(codepoint))
            {
                // Browsers fall back from an unresolved named family to the CSS initial serif default
                // for Latin/common text before they abandon family preferences entirely. Complex scripts
                // still rely on platform fallback so rows like the Arabic font fixtures can pick the same
                // per-script faces Chrome uses.
                foreach (var candidate in SkiaModel.EnumerateFontFamilyCandidates("serif", browserCompatible: true))
                {
                    if (IsGenericFamilyName(candidate))
                    {
                        continue;
                    }

                    var matchedFamily = SkiaSharp.SKFontManager.Default.MatchFamily(
                        candidate,
                        new SkiaSharp.SKFontStyle(weight, width, slant));
                    if (matchedFamily is null ||
                        matchedFamily.Handle == IntPtr.Zero ||
                        !string.Equals(matchedFamily.FamilyName, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedFamily?.Dispose();
                        continue;
                    }

                    matchedFamily.Dispose();
                    typeface = SkiaSharp.SKFontManager.Default.MatchCharacter(
                        candidate,
                        weight,
                        width,
                        slant,
                        null,
                        codepoint);

                    if (typeface is { })
                    {
                        break;
                    }
                }
            }
        }

        if (typeface is null)
        {
            typeface = SkiaSharp.SKFontManager.Default.MatchCharacter(codepoint);
        }

        if (typeface is { } && typeface.Handle == IntPtr.Zero)
        {
            typeface = null;
        }

        return typeface;
    }

    private static bool IsGenericFamilyName(string familyName)
    {
        return familyName.Equals("serif", StringComparison.OrdinalIgnoreCase) ||
               familyName.Equals("sans-serif", StringComparison.OrdinalIgnoreCase) ||
               familyName.Equals("monospace", StringComparison.OrdinalIgnoreCase) ||
               familyName.Equals("cursive", StringComparison.OrdinalIgnoreCase) ||
               familyName.Equals("fantasy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldUseSerifDefaultFallback(int codepoint)
    {
        if (codepoint <= 0x024F)
        {
            return true;
        }

        return codepoint is >= 0x0370 and <= 0x03FF ||
               codepoint is >= 0x1E00 and <= 0x1EFF;
    }

    private static List<int> CollectDistinctRenderableCodepoints(string text)
    {
        var codepoints = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if ((ch <= ' ' || ch is '\u0085' or '\u00A0') &&
                ch is not ' ' and not '\u00A0' ||
                ch >= '\u0300' && IsNonAsciiTypefaceSpanGlue(text, i, ch))
            {
                if (char.IsHighSurrogate(text[i]))
                {
                    i++;
                }

                continue;
            }

            var codepoint = GetCodepoint(text, i, ch);
            if (!codepoints.Contains(codepoint))
            {
                codepoints.Add(codepoint);
            }

            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }

        return codepoints;
    }

    private static int GetCodepoint(string text, int index, char ch)
    {
        return char.IsSurrogate(ch) ? char.ConvertToUtf32(text, index) : ch;
    }

    private static bool CanKeepGlueInCurrentTypeface(SkiaSharp.SKTypeface typeface, string text, int index, char ch)
    {
        return ch is not ' ' and not '\u00A0' ||
               typeface.ContainsGlyph(GetCodepoint(text, index, ch));
    }

    private static bool IsNonAsciiTypefaceSpanGlue(string text, int index, char ch)
    {
        if (ch is >= '\u0300' and <= '\u036F' or >= '\uFE00' and <= '\uFE0F')
        {
            return true;
        }

        if (char.IsHighSurrogate(ch))
        {
            var codepoint = char.ConvertToUtf32(text, index);
            if (codepoint is >= 0xE0100 and <= 0xE01EF)
            {
                return true;
            }
        }

        if (ch is >= '\u200B' and <= '\u200F' or >= '\u202A' and <= '\u202E' or >= '\u2060' and <= '\u206F' or '\uFEFF')
        {
            return true;
        }

        if (char.IsWhiteSpace(text, index))
        {
            return true;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(text, index);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format;
    }

    private static bool CanRenderAllCodepoints(SkiaSharp.SKTypeface? typeface, IReadOnlyList<int> codepoints)
    {
        if (typeface is null || typeface.Handle == IntPtr.Zero)
        {
            return false;
        }

        for (var i = 0; i < codepoints.Count; i++)
        {
            if (!typeface.ContainsGlyph(codepoints[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static ShimSkiaSharp.SKTypeface? ToShimTypeface(
        SkiaSharp.SKTypeface? typeface,
        SkiaSharp.SKFontStyleWeight? requestedWeight,
        string? familyNameOverride = null)
    {
        if (typeface is null || typeface.Handle == IntPtr.Zero)
        {
            return null;
        }

        var resolvedWeight = (SkiaSharp.SKFontStyleWeight)typeface.FontWeight;
        var shimWeight = requestedWeight is { } weight && resolvedWeight < weight
            ? (ShimSkiaSharp.SKFontStyleWeight)weight
            : (ShimSkiaSharp.SKFontStyleWeight)resolvedWeight;

        return ShimSkiaSharp.SKTypeface.FromFamilyName(
            familyNameOverride ?? typeface.FamilyName,
            shimWeight,
            (ShimSkiaSharp.SKFontStyleWidth)typeface.FontWidth,
            (ShimSkiaSharp.SKFontStyleSlant)typeface.FontSlant);
    }

    private SkiaSharp.SKTypeface? GetProviderTypeface(
        ITypefaceProvider provider,
        string familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant)
    {
        var key = new ProviderTypefaceKey(provider, familyName, weight, width, slant);
        if (_providerTypefaceCache.TryGetValue(key, out var cached))
        {
            if (cached is not null && cached.Handle != IntPtr.Zero)
            {
                return cached;
            }

            _providerTypefaceCache.TryRemove(key, out _);
        }

        var typeface = SharedTypefaceCache.TryGetOrAddProviderTypeface(provider, familyName, weight, width, slant, out var sharedCached)
            ? sharedCached
            : provider.FromFamilyName(familyName, weight, width, slant);
        if (typeface is { } && typeface.Handle == IntPtr.Zero)
        {
            typeface = null;
        }
        _providerTypefaceCache.TryAdd(key, typeface);
        TrimCachesIfNeeded();
        return typeface;
    }

    /// <summary>
    /// Attempts to find a typeface from custom providers that can render the specified character.
    /// </summary>
    /// <param name="familyName">The preferred font family name.</param>
    /// <param name="weight">The font weight.</param>
    /// <param name="width">The font width.</param>
    /// <param name="slant">The font slant.</param>
    /// <param name="codepoint">The character codepoint to match.</param>
    /// <returns>A matching typeface from custom providers, or null if none found.</returns>
    private SkiaSharp.SKTypeface? TryMatchCharacterFromCustomProviders(string? familyName, SkiaSharp.SKFontStyleWeight weight, SkiaSharp.SKFontStyleWidth width, SkiaSharp.SKFontStyleSlant slant, int codepoint)
    {
        return TryMatchCharacterFromCustomProviders(familyName, weight, width, slant, codepoint, out _);
    }

    private SkiaSharp.SKTypeface? TryMatchCharacterFromCustomProviders(string? familyName, SkiaSharp.SKFontStyleWeight weight, SkiaSharp.SKFontStyleWidth width, SkiaSharp.SKFontStyleSlant slant, int codepoint, out string? matchedFamily)
    {
        matchedFamily = null;
        var familyKey = familyName ?? "Default";
        foreach (var provider in _skiaModel.EnumerateEffectiveTypefaceProviders())
        {
            if (familyName is null &&
                provider is FontManagerTypefaceProvider or DefaultTypefaceProvider)
            {
                continue;
            }

            var typeface = GetProviderTypeface(provider, familyKey, weight, width, slant);
            if (typeface is { } && typeface.ContainsGlyph(codepoint))
            {
                matchedFamily = familyName;
                return typeface;
            }
        }

        return null;
    }
}
