// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

/// <summary>
/// Asset loader implementation using SkiaSharp types.
/// </summary>
public partial class SkiaSvgAssetLoader : Model.ISvgAssetLoader, Model.ISvgTextReferenceRenderingOptions, Model.ISvgTextRunTypefaceResolver, Model.ISvgTextGlyphRunResolver, Model.ISvgTextDirectedGlyphRunResolver
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

        EnsureTypefaceProviderCaches();

        var preferredTypeface = paintPreferredTypeface.Typeface;
        var weight = _skiaModel.ToSKFontStyleWeight(preferredTypeface?.FontWeight ?? ShimSkiaSharp.SKFontStyleWeight.Normal);
        var requestedWeight = preferredTypeface is null ? default(SkiaSharp.SKFontStyleWeight?) : weight;
        var width = _skiaModel.ToSKFontStyleWidth(preferredTypeface?.FontWidth ?? ShimSkiaSharp.SKFontStyleWidth.Normal);
        var slant = _skiaModel.ToSKFontStyleSlant(preferredTypeface?.FontSlant ?? ShimSkiaSharp.SKFontStyleSlant.Upright);
        var preferredFamily = GetExplicitFamilyName(preferredTypeface);
        System.Func<int, SkiaSharp.SKTypeface?> matchCharacter = codepoint =>
            MatchCharacter(preferredFamily, weight, width, slant, codepoint);

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

            ret.Add(new(currentTypefaceText, _skiaModel.GetTextAdvance(currentTypefaceText, runningPaint),
                runningPaint.Typeface is null
                    ? null
                    : ToShimTypeface(runningPaint.Typeface, requestedWeight)
            ));
        }

        for (; i < text.Length; i++)
        {
            var typeface = matchCharacter(char.ConvertToUtf32(text, i));
            if (runningPaint.Typeface is { } currentTypeface &&
                char.IsWhiteSpace(text, i))
            {
                // Keep whitespace in the active span so bidi/shaping stays attached to the
                // surrounding script run instead of splitting on a font fallback for spaces.
                typeface = currentTypeface;
            }

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

        var preferredTypeface = paintPreferredTypeface.Typeface;
        var preferredWeight = _skiaModel.ToSKFontStyleWeight(preferredTypeface?.FontWeight ?? ShimSkiaSharp.SKFontStyleWeight.Normal);
        var requestedWeight = preferredTypeface is null ? default(SkiaSharp.SKFontStyleWeight?) : preferredWeight;
        var preferredWidth = _skiaModel.ToSKFontStyleWidth(preferredTypeface?.FontWidth ?? ShimSkiaSharp.SKFontStyleWidth.Normal);
        var preferredSlant = _skiaModel.ToSKFontStyleSlant(preferredTypeface?.FontSlant ?? ShimSkiaSharp.SKFontStyleSlant.Upright);
        var preferredFamily = GetExplicitFamilyName(preferredTypeface);

        var candidates = new List<SkiaSharp.SKTypeface?>();
        void AddCandidate(SkiaSharp.SKTypeface? candidate)
        {
            if (candidate is null || candidate.Handle == IntPtr.Zero)
            {
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var existing = candidates[i];
                if (existing is not null &&
                    (existing.FamilyName, existing.FontWeight, existing.FontWidth, existing.FontSlant) ==
                    (candidate.FamilyName, candidate.FontWeight, candidate.FontWidth, candidate.FontSlant))
                {
                    return;
                }
            }

            candidates.Add(candidate);
        }

        using var preferredPaint = _skiaModel.ToSKPaint(paintPreferredTypeface);
        AddCandidate(preferredPaint?.Typeface);

        var spans = FindTypefaces(text, paintPreferredTypeface);
        for (var i = 0; i < spans.Count; i++)
        {
            if (spans[i].Typeface is { } spanTypeface)
            {
                AddCandidate(_skiaModel.ToSKTypeface(spanTypeface));
            }
        }

        for (var i = 0; i < codepoints.Count; i++)
        {
            AddCandidate(MatchCharacter(preferredFamily, preferredWeight, preferredWidth, preferredSlant, codepoints[i]));
            AddCandidate(MatchCharacter(null, preferredWeight, preferredWidth, preferredSlant, codepoints[i]));
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (CanRenderAllCodepoints(candidate, codepoints))
            {
                return ToShimTypeface(candidate, requestedWeight);
            }
        }

        return null;
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
        var skPaint = GetCachedPaint(paint);
        if (skPaint is null || text is null)
        {
            bounds = default;
            return 0f;
        }

        var skBounds = new SkiaSharp.SKRect();
        skPaint.MeasureText(text, ref skBounds);
        var width = _skiaModel.GetTextAdvance(text, skPaint);
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
    public ShimSkiaSharp.SKPath? GetTextPath(string? text, ShimSkiaSharp.SKPaint paint, float x, float y)
    {
        var skPaint = GetCachedPaint(paint);
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
        var hash = ComputeTypefaceProviderHash(providers);
        if (!ReferenceEquals(providers, _providerStateList) || hash != _providerStateHash)
        {
            _providerStateList = providers;
            _providerStateHash = hash;
            _matchCharacterCache.Clear();
            _providerTypefaceCache.Clear();
            ClearPaintCache();
        }
    }

    private static int ComputeTypefaceProviderHash(IList<ITypefaceProvider>? providers)
    {
        unchecked
        {
            var hash = 17;
            if (providers is null)
            {
                return hash;
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
        if (typeface is null && ShouldUsePlatformCharacterFallback(normalizedFamily, codepoint))
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

    private bool ShouldUsePlatformCharacterFallback(string? familyName, int codepoint)
    {
        return familyName is not null || codepoint > 0x007F || HasNonPlatformTypefaceProviders();
    }

    private static string? GetExplicitFamilyName(ShimSkiaSharp.SKTypeface? typeface)
    {
        return SkiaModel.HasExplicitTypeface(typeface) ? typeface!.FamilyName : null;
    }

    private bool HasNonPlatformTypefaceProviders()
    {
        var providers = _skiaModel.Settings.TypefaceProviders;
        if (providers is null)
        {
            return false;
        }

        for (var i = 0; i < providers.Count; i++)
        {
            if (providers[i] is not FontManagerTypefaceProvider and not DefaultTypefaceProvider)
            {
                return true;
            }
        }

        return false;
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
            var codepoint = char.ConvertToUtf32(text, i);
            if (char.IsWhiteSpace(text, i))
            {
                if (char.IsHighSurrogate(text[i]))
                {
                    i++;
                }

                continue;
            }

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
        SkiaSharp.SKFontStyleWeight? requestedWeight)
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
            typeface.FamilyName,
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
        if (_skiaModel.Settings.TypefaceProviders is null || _skiaModel.Settings.TypefaceProviders.Count == 0)
        {
            return null;
        }

        var familyKey = familyName ?? "Default";
        foreach (var provider in _skiaModel.Settings.TypefaceProviders)
        {
            if (familyName is null &&
                provider is FontManagerTypefaceProvider or DefaultTypefaceProvider)
            {
                continue;
            }

            var typeface = GetProviderTypeface(provider, familyKey, weight, width, slant);
            if (typeface is { } && typeface.ContainsGlyph(codepoint))
            {
                return typeface;
            }
        }

        return null;
    }
}
