// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

/// <summary>
/// Asset loader implementation using SkiaSharp types.
/// </summary>
public partial class SkiaSvgAssetLoader : Model.ISvgAssetLoader, Model.ISvgTextMeasurementCacheKeyProvider, Model.ISvgTextReferenceRenderingOptions, Model.ISvgTextRunTypefaceResolver, Model.ISvgTextGlyphRunResolver, Model.ISvgTextDirectedGlyphRunResolver
{
    private readonly SkiaModel _skiaModel;
    private const int SharedTextMeasurementCacheKey = 0x53A2C1D;

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
    int Model.ISvgTextMeasurementCacheKeyProvider.TextMeasurementCacheKey => GetTextMeasurementCacheKey();

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

        var typefaceSpanCache = GetTypefaceSpanCache();
        var cacheKey = new TypefaceSpanCacheKey(text, paintPreferredTypeface);
        if (typefaceSpanCache.TryGetValue(cacheKey, out var cachedSpans))
        {
            return new List<Model.TypefaceSpan>(cachedSpans);
        }

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

        if (TryCreateSingleTypefaceSpan(text, runningPaint, out var singleSpan))
        {
            ret.Add(singleSpan);
            typefaceSpanCache[cacheKey] = ret.ToArray();
            TrimCachesIfNeeded();
            return ret;
        }

        var currentTypefaceStartIndex = 0;
        var i = 0;

        void YieldCurrentTypefaceText()
        {
            var currentTypefaceText = text.Substring(currentTypefaceStartIndex, i - currentTypefaceStartIndex);

            ret.Add(new(currentTypefaceText, _skiaModel.GetTextAdvance(currentTypefaceText, runningPaint),
                ToShimTypeface(runningPaint.Typeface)
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

        typefaceSpanCache[cacheKey] = ret.ToArray();
        TrimCachesIfNeeded();

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
        var preferredWidth = _skiaModel.ToSKFontStyleWidth(preferredTypeface?.FontWidth ?? ShimSkiaSharp.SKFontStyleWidth.Normal);
        var preferredSlant = _skiaModel.ToSKFontStyleSlant(preferredTypeface?.FontSlant ?? ShimSkiaSharp.SKFontStyleSlant.Upright);
        var preferredFamily = preferredTypeface?.FamilyName;

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
            AddCandidate(_skiaModel.ToSKTypeface(spans[i].Typeface));
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
                return ToShimTypeface(candidate);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public ShimSkiaSharp.SKFontMetrics GetFontMetrics(ShimSkiaSharp.SKPaint paint)
    {
        var skPaint = GetCachedPaint(paint);
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
        var skPaint = GetCachedPaint(paint);
        if (skPaint is null)
        {
            shapedRun = default;
            return false;
        }

        return _skiaModel.TryShapeGlyphRun(text, skPaint, rightToLeft: null, out shapedRun);
    }

    /// <inheritdoc />
    public bool TryShapeGlyphRun(string? text, ShimSkiaSharp.SKPaint paint, bool rightToLeft, out Model.ShapedGlyphRun shapedRun)
    {
        var skPaint = GetCachedPaint(paint);
        if (skPaint is null)
        {
            shapedRun = default;
            return false;
        }

        return _skiaModel.TryShapeGlyphRun(text, skPaint, rightToLeft, out shapedRun);
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
        var providers = _skiaModel.Settings.ConfiguredTypefaceProviders;
        var hash = ComputeTypefaceProviderHash(providers);
        if (!ReferenceEquals(providers, _providerStateList) || hash != _providerStateHash)
        {
            _providerStateList = providers;
            _providerStateHash = hash;
            _matchCharacterCache?.Clear();
            _providerTypefaceCache?.Clear();
            _typefaceSpanCache?.Clear();
            ClearPaintCache();
        }
    }

    private bool UsesSharedTypefaceCaches()
    {
        var providers = _skiaModel.Settings.ConfiguredTypefaceProviders;
        return (providers is null || providers.Count == 0) &&
               !_skiaModel.Settings.EnableSvgFonts &&
               !_skiaModel.Settings.EnableTextReferences;
    }

    private int GetTextMeasurementCacheKey()
    {
        var providers = _skiaModel.Settings.ConfiguredTypefaceProviders;
        if ((providers is null || providers.Count == 0) &&
            !_skiaModel.Settings.EnableSvgFonts &&
            !_skiaModel.Settings.EnableTextReferences)
        {
            return SharedTextMeasurementCacheKey;
        }

        unchecked
        {
            return (RuntimeHelpers.GetHashCode(this) * 397) ^ ComputeTypefaceProviderHash(providers);
        }
    }

    private static bool CanCachePaint(ShimSkiaSharp.SKPaint paint)
    {
        return paint.Shader is null
            && paint.ColorFilter is null
            && paint.ImageFilter is null
            && paint.PathEffect is null;
    }

    private bool CanUseSharedPaintTemplates(ShimSkiaSharp.SKPaint paint)
    {
        return CanCachePaint(paint) &&
               (paint.Typeface is null || UsesSharedTypefaceCaches());
    }

    private ConcurrentDictionary<MatchCharacterKey, SkiaSharp.SKTypeface?> GetMatchCharacterCache()
    {
        return UsesSharedTypefaceCaches()
            ? s_sharedMatchCharacterCache
            : LazyInitializer.EnsureInitialized(ref _matchCharacterCache)!;
    }

    private ConcurrentDictionary<ProviderTypefaceKey, SkiaSharp.SKTypeface?> GetProviderTypefaceCache()
    {
        return UsesSharedTypefaceCaches()
            ? s_sharedProviderTypefaceCache
            : LazyInitializer.EnsureInitialized(ref _providerTypefaceCache)!;
    }

    private ConcurrentDictionary<TypefaceSpanCacheKey, Model.TypefaceSpan[]> GetTypefaceSpanCache()
    {
        return UsesSharedTypefaceCaches()
            ? s_sharedTypefaceSpanCache
            : LazyInitializer.EnsureInitialized(ref _typefaceSpanCache)!;
    }

    private object GetPaintCacheLock()
    {
        return LazyInitializer.EnsureInitialized(ref _paintCacheLock, static () => new object())!;
    }

    private ConditionalWeakTable<ShimSkiaSharp.SKPaint, CachedSkPaint> GetPaintCache()
    {
        return LazyInitializer.EnsureInitialized(
            ref _paintCache,
            static () => new ConditionalWeakTable<ShimSkiaSharp.SKPaint, CachedSkPaint>())!;
    }

    private List<WeakReference<SkiaSharp.SKPaint>> GetPaintCacheRefs()
    {
        return LazyInitializer.EnsureInitialized(
            ref _paintCacheRefs,
            static () => new List<WeakReference<SkiaSharp.SKPaint>>())!;
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
        var paintCacheLock = _paintCacheLock;
        if (paintCacheLock is null)
        {
            return;
        }

        lock (paintCacheLock)
        {
            if (_paintCacheRefs is { } paintCacheRefs)
            {
                foreach (var weak in paintCacheRefs)
                {
                    if (weak.TryGetTarget(out var paint) && paint.Handle != IntPtr.Zero)
                    {
                        paint.Dispose();
                    }
                }

                paintCacheRefs.Clear();
            }

            _paintCache = null;
        }
    }

    private static SharedPaintTemplate CreateSharedPaintTemplate(SkiaSharp.SKPaint paint)
    {
        return new SharedPaintTemplate(
            paint.Style,
            paint.IsAntialias,
            paint.StrokeWidth,
            paint.StrokeCap,
            paint.StrokeJoin,
            paint.StrokeMiter,
            paint.TextSize,
            paint.TextAlign,
            paint.Typeface,
            paint.LcdRenderText,
            paint.SubpixelText,
            paint.TextEncoding,
            paint.Color,
            paint.BlendMode,
            paint.FilterQuality,
            paint.FakeBoldText);
    }

    private static SkiaSharp.SKPaint CreatePaint(in SharedPaintTemplate template)
    {
        return new SkiaSharp.SKPaint
        {
            Style = template.Style,
            IsAntialias = template.IsAntialias,
            StrokeWidth = template.StrokeWidth,
            StrokeCap = template.StrokeCap,
            StrokeJoin = template.StrokeJoin,
            StrokeMiter = template.StrokeMiter,
            TextSize = template.TextSize,
            TextAlign = template.TextAlign,
            Typeface = template.Typeface,
            LcdRenderText = template.LcdRenderText,
            SubpixelText = template.SubpixelText,
            TextEncoding = template.TextEncoding,
            Color = template.Color,
            BlendMode = template.BlendMode,
            FilterQuality = template.FilterQuality,
            FakeBoldText = template.FakeBoldText
        };
    }

    private static void TrimSharedPaintTemplateCacheIfNeeded()
    {
        if (s_sharedPaintTemplateCache.Count > SharedPaintTemplateCacheLimit)
        {
            s_sharedPaintTemplateCache.Clear();
        }
    }

    private static void TrimPaintCacheRefsIfNeeded(List<WeakReference<SkiaSharp.SKPaint>> paintCacheRefs)
    {
        if (paintCacheRefs.Count <= PaintCacheRefTrimThreshold)
        {
            return;
        }

        for (var i = paintCacheRefs.Count - 1; i >= 0; i--)
        {
            var weak = paintCacheRefs[i];
            if (!weak.TryGetTarget(out var paint) || paint.Handle == IntPtr.Zero)
            {
                paintCacheRefs.RemoveAt(i);
            }
        }
    }

    private SkiaSharp.SKPaint? GetCachedPaint(ShimSkiaSharp.SKPaint paint)
    {
        EnsureTypefaceProviderCaches();

        var signature = new PaintSignature(paint);
        lock (GetPaintCacheLock())
        {
            var paintCache = GetPaintCache();
            if (paintCache.TryGetValue(paint, out var cached))
            {
                if (cached.Paint.Handle != IntPtr.Zero && cached.Signature.Equals(signature))
                {
                    return cached.Paint;
                }

                cached.Dispose();
                paintCache.Remove(paint);
            }

            SkiaSharp.SKPaint? skPaint;
            if (CanUseSharedPaintTemplates(paint) &&
                s_sharedPaintTemplateCache.TryGetValue(signature, out var sharedTemplate))
            {
                skPaint = CreatePaint(sharedTemplate);
            }
            else
            {
                skPaint = _skiaModel.ToSKPaint(paint);
                if (skPaint is not null && CanUseSharedPaintTemplates(paint))
                {
                    s_sharedPaintTemplateCache[signature] = CreateSharedPaintTemplate(skPaint);
                    TrimSharedPaintTemplateCacheIfNeeded();
                }
            }

            if (skPaint is null)
            {
                return null;
            }

            paintCache.Add(paint, new CachedSkPaint(signature, skPaint));
            var paintCacheRefs = GetPaintCacheRefs();
            paintCacheRefs.Add(new WeakReference<SkiaSharp.SKPaint>(skPaint));
            TrimPaintCacheRefsIfNeeded(paintCacheRefs);
            return skPaint;
        }
    }

    private void TrimCachesIfNeeded()
    {
        var matchCharacterCache = GetMatchCharacterCache();
        if (matchCharacterCache.Count > MatchCharacterCacheLimit)
        {
            matchCharacterCache.Clear();
        }

        var providerTypefaceCache = GetProviderTypefaceCache();
        if (providerTypefaceCache.Count > ProviderTypefaceCacheLimit)
        {
            providerTypefaceCache.Clear();
        }

        var typefaceSpanCache = GetTypefaceSpanCache();
        if (typefaceSpanCache.Count > TypefaceSpanCacheLimit)
        {
            typefaceSpanCache.Clear();
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
        var matchCharacterCache = GetMatchCharacterCache();
        if (matchCharacterCache.TryGetValue(key, out var cached))
        {
            if (cached is not null && cached.Handle != IntPtr.Zero)
            {
                return cached;
            }

            matchCharacterCache.TryRemove(key, out _);
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

        matchCharacterCache.TryAdd(key, typeface);
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

    private static List<int> CollectDistinctRenderableCodepoints(string text)
    {
        var codepoints = new List<int>();
        var seenCodepoints = new HashSet<int>();
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

            if (seenCodepoints.Add(codepoint))
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

    private Model.TypefaceSpan CreateTypefaceSpan(string text, SkiaSharp.SKPaint paint)
    {
        return new Model.TypefaceSpan(text, _skiaModel.GetTextAdvance(text, paint), ToShimTypeface(paint.Typeface));
    }

    private bool TryCreateSingleTypefaceSpan(string text, SkiaSharp.SKPaint paint, out Model.TypefaceSpan span)
    {
        span = default;

        if (paint.Typeface is not { } typeface || typeface.Handle == IntPtr.Zero)
        {
            return false;
        }

        var codepoints = CollectDistinctRenderableCodepoints(text);
        if (codepoints.Count > 0 && !CanRenderAllCodepoints(typeface, codepoints))
        {
            return false;
        }

        span = CreateTypefaceSpan(text, paint);
        return true;
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

    private static ShimSkiaSharp.SKTypeface? ToShimTypeface(SkiaSharp.SKTypeface? typeface)
    {
        return typeface is null || typeface.Handle == IntPtr.Zero
            ? null
            : ShimSkiaSharp.SKTypeface.FromFamilyName(
                typeface.FamilyName,
                (ShimSkiaSharp.SKFontStyleWeight)typeface.FontWeight,
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
        var providerTypefaceCache = GetProviderTypefaceCache();
        if (providerTypefaceCache.TryGetValue(key, out var cached))
        {
            if (cached is not null && cached.Handle != IntPtr.Zero)
            {
                return cached;
            }

            providerTypefaceCache.TryRemove(key, out _);
        }

        var typeface = provider.FromFamilyName(familyName, weight, width, slant);
        if (typeface is { } && typeface.Handle == IntPtr.Zero)
        {
            typeface = null;
        }
        providerTypefaceCache.TryAdd(key, typeface);
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
        var providers = _skiaModel.Settings.ConfiguredTypefaceProviders;
        if (providers is null || providers.Count == 0)
        {
            return null;
        }

        var familyKey = familyName ?? "Default";
        foreach (var provider in providers)
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
