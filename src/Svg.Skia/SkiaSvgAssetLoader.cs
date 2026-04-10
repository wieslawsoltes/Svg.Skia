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
public partial class SkiaSvgAssetLoader : Model.ISvgAssetLoader
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
        var width = _skiaModel.ToSKFontStyleWidth(preferredTypeface?.FontWidth ?? ShimSkiaSharp.SKFontStyleWidth.Normal);
        var slant = _skiaModel.ToSKFontStyleSlant(preferredTypeface?.FontSlant ?? ShimSkiaSharp.SKFontStyleSlant.Upright);
        var preferredFamily = preferredTypeface?.FamilyName;
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
                else if (provider is FontManagerTypefaceProvider fontManagerProvider)
                {
                    hash = (hash * 397) ^ (fontManagerProvider.FontManager?.Handle.GetHashCode() ?? 0);
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
        if (typeface is null && normalizedFamily is not null)
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

        _matchCharacterCache.TryAdd(key, typeface);
        TrimCachesIfNeeded();
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

        var typeface = provider.FromFamilyName(familyName, weight, width, slant);
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
            var typeface = GetProviderTypeface(provider, familyKey, weight, width, slant);
            if (typeface is { } && typeface.ContainsGlyph(codepoint))
            {
                return typeface;
            }
        }

        return null;
    }
}
