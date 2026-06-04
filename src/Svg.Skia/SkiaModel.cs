// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ShimSkiaSharp;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

public partial class SkiaModel
{
    private static readonly char[] s_fontFamilyTrimChars = { '\'', '"' };

    private sealed class DrawPictureState
    {
        private int _saveDepth;
        private int _saveLayerDepth;
        private int _singleLayerSaveDepth;
        private List<int>? _nestedLayerSaveDepths;

        public int SaveLayerDepth => _saveLayerDepth;

        public void Save(bool isLayer)
        {
            _saveDepth++;
            if (isLayer)
            {
                _saveLayerDepth++;
                if (_saveLayerDepth == 1)
                {
                    _singleLayerSaveDepth = _saveDepth;
                }
                else
                {
                    (_nestedLayerSaveDepths ??= new List<int>(2)).Add(_saveDepth);
                }
            }
        }

        public void Restore()
        {
            if (_saveDepth == 0)
            {
                return;
            }

            if (_saveLayerDepth > 0 && CurrentLayerSaveDepth == _saveDepth)
            {
                if (_saveLayerDepth == 1)
                {
                    _singleLayerSaveDepth = 0;
                }
                else
                {
                    _nestedLayerSaveDepths!.RemoveAt(_saveLayerDepth - 2);
                }

                _saveLayerDepth--;
            }

            _saveDepth--;
        }

        private int CurrentLayerSaveDepth
            => _saveLayerDepth == 1 ? _singleLayerSaveDepth : _nestedLayerSaveDepths![_saveLayerDepth - 2];
    }

    private static readonly Dictionary<string, string[]> s_genericFontFamilyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sans-serif"] = new[] { "sans-serif", "Helvetica Neue", "Helvetica", "Arial", "Roboto", "Segoe UI", "DejaVu Sans" },
        ["serif"] = new[] { "serif", "Times New Roman", "Times", "Georgia", "Droid Serif", "DejaVu Serif" },
        ["monospace"] = new[] { "monospace", "Courier New", "Courier", "Menlo", "Consolas", "Roboto Mono", "DejaVu Sans Mono" },
        ["cursive"] = new[] { "cursive", "Snell Roundhand", "Comic Sans MS", "Apple Chancery" },
        ["fantasy"] = new[] { "fantasy", "Impact", "Papyrus" }
    };

    private static readonly Dictionary<string, string[]> s_browserCompatibleGenericFontFamilyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sans-serif"] = new[] { "sans-serif", "Helvetica", "Helvetica Neue", "Arial", "Roboto", "Segoe UI", "DejaVu Sans" },
        ["serif"] = new[] { "serif", "Times", "Times New Roman", "Georgia", "Droid Serif", "DejaVu Serif" },
        ["monospace"] = new[] { "monospace", "Courier New", "Courier", "Menlo", "Consolas", "Roboto Mono", "DejaVu Sans Mono" },
        ["cursive"] = new[] { "cursive", "Snell Roundhand", "Comic Sans MS", "Apple Chancery" },
        ["fantasy"] = new[] { "fantasy", "Impact", "Papyrus" }
    };
    public SKSvgSettings Settings { get; }

    public SkiaModel(SKSvgSettings settings)
    {
        Settings = settings;
    }

    public SkiaSharp.SKPoint ToSKPoint(SKPoint point)
    {
        return new(point.X, point.Y);
    }

    public SkiaSharp.SKPoint3 ToSKPoint3(SKPoint3 point3)
    {
        return new(point3.X, point3.Y, point3.Z);
    }

    public SkiaSharp.SKPoint[] ToSKPoints(IList<SKPoint> points)
    {
        var skPoints = new SkiaSharp.SKPoint[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            skPoints[i] = ToSKPoint(points[i]);
        }

        return skPoints;
    }

    public SkiaSharp.SKPointI ToSKPointI(SKPointI pointI)
    {
        return new(pointI.X, pointI.Y);
    }

    public SkiaSharp.SKSize ToSKSize(SKSize size)
    {
        return new(size.Width, size.Height);
    }

    public SkiaSharp.SKSizeI ToSKSizeI(SKSizeI sizeI)
    {
        return new(sizeI.Width, sizeI.Height);
    }

    public SkiaSharp.SKRect ToSKRect(SKRect rect)
    {
        return new(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public SkiaSharp.SKMatrix ToSKMatrix(SKMatrix matrix)
    {
        return new(
            matrix.ScaleX,
            matrix.SkewX,
            matrix.TransX,
            matrix.SkewY,
            matrix.ScaleY,
            matrix.TransY,
            matrix.Persp0,
            matrix.Persp1,
            matrix.Persp2);
    }

    public SkiaSharp.SKImage ToSKImage(SKImage image)
    {
        return SkiaSharp.SKImage.FromEncodedData(image.Data);
    }

    public SkiaSharp.SKPaintStyle ToSKPaintStyle(SKPaintStyle paintStyle)
    {
        return paintStyle switch
        {
            SKPaintStyle.Fill => SkiaSharp.SKPaintStyle.Fill,
            SKPaintStyle.Stroke => SkiaSharp.SKPaintStyle.Stroke,
            SKPaintStyle.StrokeAndFill => SkiaSharp.SKPaintStyle.StrokeAndFill,
            _ => SkiaSharp.SKPaintStyle.Fill
        };
    }

    public SkiaSharp.SKStrokeCap ToSKStrokeCap(SKStrokeCap strokeCap)
    {
        return strokeCap switch
        {
            SKStrokeCap.Butt => SkiaSharp.SKStrokeCap.Butt,
            SKStrokeCap.Round => SkiaSharp.SKStrokeCap.Round,
            SKStrokeCap.Square => SkiaSharp.SKStrokeCap.Square,
            _ => SkiaSharp.SKStrokeCap.Butt
        };
    }

    public SkiaSharp.SKStrokeJoin ToSKStrokeJoin(SKStrokeJoin strokeJoin)
    {
        return strokeJoin switch
        {
            SKStrokeJoin.Miter => SkiaSharp.SKStrokeJoin.Miter,
            SKStrokeJoin.Round => SkiaSharp.SKStrokeJoin.Round,
            SKStrokeJoin.Bevel => SkiaSharp.SKStrokeJoin.Bevel,
            _ => SkiaSharp.SKStrokeJoin.Miter
        };
    }

    public SkiaSharp.SKTextAlign ToSKTextAlign(SKTextAlign textAlign)
    {
        return textAlign switch
        {
            SKTextAlign.Left => SkiaSharp.SKTextAlign.Left,
            SKTextAlign.Center => SkiaSharp.SKTextAlign.Center,
            SKTextAlign.Right => SkiaSharp.SKTextAlign.Right,
            _ => SkiaSharp.SKTextAlign.Left
        };
    }

    public SkiaSharp.SKTextEncoding ToSKTextEncoding(SKTextEncoding textEncoding)
    {
        return textEncoding switch
        {
            SKTextEncoding.Utf8 => SkiaSharp.SKTextEncoding.Utf8,
            SKTextEncoding.Utf16 => SkiaSharp.SKTextEncoding.Utf16,
            SKTextEncoding.Utf32 => SkiaSharp.SKTextEncoding.Utf32,
            SKTextEncoding.GlyphId => SkiaSharp.SKTextEncoding.GlyphId,
            _ => SkiaSharp.SKTextEncoding.Utf8
        };
    }

    public SkiaSharp.SKFontEdging ToSKFontEdging(SKFontEdging edging)
    {
        return edging switch
        {
            SKFontEdging.Alias => SkiaSharp.SKFontEdging.Alias,
            SKFontEdging.Antialias => SkiaSharp.SKFontEdging.Antialias,
            SKFontEdging.SubpixelAntialias => SkiaSharp.SKFontEdging.SubpixelAntialias,
            _ => SkiaSharp.SKFontEdging.Antialias
        };
    }

    private static SkiaSharp.SKFontEdging ToSKFontEdging(SKPaint paint)
    {
        if (!paint.IsAntialias)
        {
            return SkiaSharp.SKFontEdging.Alias;
        }

        return paint.LcdRenderText
            ? SkiaSharp.SKFontEdging.SubpixelAntialias
            : SkiaSharp.SKFontEdging.Antialias;
    }

    public SkiaSharp.SKFontStyleWeight ToSKFontStyleWeight(SKFontStyleWeight fontStyleWeight)
    {
        return fontStyleWeight switch
        {
            SKFontStyleWeight.Invisible => SkiaSharp.SKFontStyleWeight.Invisible,
            SKFontStyleWeight.Thin => SkiaSharp.SKFontStyleWeight.Thin,
            SKFontStyleWeight.ExtraLight => SkiaSharp.SKFontStyleWeight.ExtraLight,
            SKFontStyleWeight.Light => SkiaSharp.SKFontStyleWeight.Light,
            SKFontStyleWeight.Normal => SkiaSharp.SKFontStyleWeight.Normal,
            SKFontStyleWeight.Medium => SkiaSharp.SKFontStyleWeight.Medium,
            SKFontStyleWeight.SemiBold => SkiaSharp.SKFontStyleWeight.SemiBold,
            SKFontStyleWeight.Bold => SkiaSharp.SKFontStyleWeight.Bold,
            SKFontStyleWeight.ExtraBold => SkiaSharp.SKFontStyleWeight.ExtraBold,
            SKFontStyleWeight.Black => SkiaSharp.SKFontStyleWeight.Black,
            SKFontStyleWeight.ExtraBlack => SkiaSharp.SKFontStyleWeight.ExtraBlack,
            _ => SkiaSharp.SKFontStyleWeight.Invisible
        };
    }

    public SkiaSharp.SKFontStyleWidth ToSKFontStyleWidth(SKFontStyleWidth fontStyleWidth)
    {
        return fontStyleWidth switch
        {
            SKFontStyleWidth.UltraCondensed => SkiaSharp.SKFontStyleWidth.UltraCondensed,
            SKFontStyleWidth.ExtraCondensed => SkiaSharp.SKFontStyleWidth.ExtraCondensed,
            SKFontStyleWidth.Condensed => SkiaSharp.SKFontStyleWidth.Condensed,
            SKFontStyleWidth.SemiCondensed => SkiaSharp.SKFontStyleWidth.SemiCondensed,
            SKFontStyleWidth.Normal => SkiaSharp.SKFontStyleWidth.Normal,
            SKFontStyleWidth.SemiExpanded => SkiaSharp.SKFontStyleWidth.SemiExpanded,
            SKFontStyleWidth.Expanded => SkiaSharp.SKFontStyleWidth.Expanded,
            SKFontStyleWidth.ExtraExpanded => SkiaSharp.SKFontStyleWidth.ExtraExpanded,
            SKFontStyleWidth.UltraExpanded => SkiaSharp.SKFontStyleWidth.UltraExpanded,
            _ => SkiaSharp.SKFontStyleWidth.UltraCondensed
        };
    }

    public SkiaSharp.SKFontStyleSlant ToSKFontStyleSlant(SKFontStyleSlant fontStyleSlant)
    {
        return fontStyleSlant switch
        {
            SKFontStyleSlant.Upright => SkiaSharp.SKFontStyleSlant.Upright,
            SKFontStyleSlant.Italic => SkiaSharp.SKFontStyleSlant.Italic,
            SKFontStyleSlant.Oblique => SkiaSharp.SKFontStyleSlant.Oblique,
            _ => SkiaSharp.SKFontStyleSlant.Upright
        };
    }

    internal static IEnumerable<string> EnumerateFontFamilyCandidates(string? fontFamily, bool browserCompatible = false)
    {
        var genericFontFamilyMap = browserCompatible
            ? s_browserCompatibleGenericFontFamilyMap
            : s_genericFontFamilyMap;
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            foreach (var rawFamily in fontFamily!.Split(','))
            {
                var candidate = rawFamily.Trim();
                if (candidate.Length == 0)
                {
                    continue;
                }

                candidate = candidate.Trim(s_fontFamilyTrimChars);
                if (candidate.Length == 0 || !yielded.Add(candidate))
                {
                    continue;
                }

                yield return candidate;

                if (genericFontFamilyMap.TryGetValue(candidate, out var mappedFamilies))
                {
                    foreach (var mapped in mappedFamilies)
                    {
                        if (yielded.Add(mapped))
                        {
                            yield return mapped;
                        }
                    }
                }
            }
        }

        if (yielded.Count == 0 && genericFontFamilyMap.TryGetValue("sans-serif", out var fallbackFamilies))
        {
            foreach (var mapped in fallbackFamilies)
            {
                if (yielded.Add(mapped))
                {
                    yield return mapped;
                }
            }
        }
    }

    private void EnsureTypefaceProviderCaches()
    {
        var providers = Settings.TypefaceProviders;
        var documentProviders = Settings.DocumentTypefaceProviders;
        var hash = ComputeTypefaceProviderHash(documentProviders, providers);
        if (!ReferenceEquals(providers, _providerStateList) || hash != _providerStateHash)
        {
            _providerStateList = providers;
            _providerStateHash = hash;
            _typefaceCache.Clear();
            _resolvedTypefaceCache.Clear();
            ClearPositionedTextCache();
            ClearReusableRenderCaches();
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

    internal IEnumerable<ITypefaceProvider> EnumerateEffectiveTypefaceProviders()
    {
        if (Settings.DocumentTypefaceProviders is { } documentProviders)
        {
            for (var i = 0; i < documentProviders.Count; i++)
            {
                yield return documentProviders[i];
            }
        }

        if (Settings.TypefaceProviders is { } providers)
        {
            for (var i = 0; i < providers.Count; i++)
            {
                yield return providers[i];
            }
        }
    }

    private void ClearPositionedTextCache()
    {
        lock (_positionedTextCacheLock)
        {
            foreach (var weak in _positionedTextCacheRefs)
            {
                if (weak.TryGetTarget(out var textBlob) && textBlob.Handle != IntPtr.Zero)
                {
                    textBlob.Dispose();
                }
            }

            _positionedTextCacheRefs.Clear();
            _positionedTextCache = new ConditionalWeakTable<DrawTextBlobCanvasCommand, PositionedTextCache>();
            _shapedTextCache = null;
            _lastConvertedPicture = null;
            _previousConvertedPicture = null;
        }
    }

    private void TrimPositionedTextCacheRefsIfNeeded()
    {
        if (_positionedTextCacheRefs.Count <= PositionedTextCacheRefTrimThreshold)
        {
            return;
        }

        for (var i = _positionedTextCacheRefs.Count - 1; i >= 0; i--)
        {
            var weak = _positionedTextCacheRefs[i];
            if (!weak.TryGetTarget(out var textBlob) || textBlob.Handle == IntPtr.Zero)
            {
                _positionedTextCacheRefs.RemoveAt(i);
            }
        }
    }

    private void TrimTypefaceCachesIfNeeded()
    {
        if (_typefaceCache.Count > TypefaceCacheLimit)
        {
            _typefaceCache.Clear();
        }

        if (_resolvedTypefaceCache.Count > ResolvedTypefaceCacheLimit)
        {
            _resolvedTypefaceCache.Clear();
        }
    }

    private SkiaSharp.SKTypeface? ResolveTypeface(string candidate, SkiaSharp.SKFontStyle style)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return null;
        }

        var weight = (SkiaSharp.SKFontStyleWeight)style.Weight;
        var width = (SkiaSharp.SKFontStyleWidth)style.Width;
        var slant = (SkiaSharp.SKFontStyleSlant)style.Slant;
        var cacheKey = new TypefaceKey(
            candidate,
            weight,
            width,
            slant);
        if (_resolvedTypefaceCache.TryGetValue(cacheKey, out var cached))
        {
            if (cached is not null && cached.Handle != IntPtr.Zero)
            {
                return cached;
            }

            _resolvedTypefaceCache.TryRemove(cacheKey, out _);
        }

        if (SharedTypefaceCache.TryGetResolvedTypeface(candidate, weight, width, slant, out var sharedCached))
        {
            if (sharedCached is not null)
            {
                _resolvedTypefaceCache.TryAdd(cacheKey, sharedCached);
                TrimTypefaceCachesIfNeeded();
            }

            return sharedCached;
        }

        var fontManager = SkiaSharp.SKFontManager.Default;
        var resolved = default(SkiaSharp.SKTypeface);

        var matched = fontManager.MatchFamily(candidate, style);
        if (matched is { } && matched.Handle != IntPtr.Zero)
        {
            if (IsGenericFontFamilyName(candidate) ||
                string.Equals(matched.FamilyName, candidate, StringComparison.OrdinalIgnoreCase))
            {
                resolved = matched;
            }
            else
            {
                matched.Dispose();
            }
        }

        if (resolved is null)
        {
            var requested = SkiaSharp.SKTypeface.FromFamilyName(candidate, style.Weight, style.Width, style.Slant);
            if (requested is { } && requested.Handle != IntPtr.Zero &&
                (IsGenericFontFamilyName(candidate) ||
                  string.Equals(requested.FamilyName, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                resolved = requested;
            }
            else
            {
                requested?.Dispose();
            }
        }

        if (resolved is not null)
        {
            _resolvedTypefaceCache.TryAdd(cacheKey, resolved);
            TrimTypefaceCachesIfNeeded();
        }

        SharedTypefaceCache.AddResolvedTypeface(candidate, weight, width, slant, resolved);
        return resolved;
    }

    private static SkiaSharp.SKTypeface? ResolveProviderTypeface(
        ITypefaceProvider typefaceProvider,
        string candidate,
        SkiaSharp.SKFontStyleWeight fontWeight,
        SkiaSharp.SKFontStyleWidth fontWidth,
        SkiaSharp.SKFontStyleSlant fontStyle)
    {
        var typeface = SharedTypefaceCache.TryGetOrAddProviderTypeface(
            typefaceProvider,
            candidate,
            fontWeight,
            fontWidth,
            fontStyle,
            out var cached)
            ? cached
            : typefaceProvider.FromFamilyName(candidate, fontWeight, fontWidth, fontStyle);

        return typeface is { } && typeface.Handle == IntPtr.Zero
            ? null
            : typeface;
    }

    public SkiaSharp.SKTypeface? ToSKTypeface(SKTypeface? typeface)
    {
        return ResolveSKTypeface(typeface).Typeface;
    }

    private TypefaceResolution ResolveSKTypeface(SKTypeface? typeface)
    {
        var fontFamily = typeface?.FamilyName;
        var fontWeight = ToSKFontStyleWeight(typeface?.FontWeight ?? SKFontStyleWeight.Normal);
        var fontWidth = ToSKFontStyleWidth(typeface?.FontWidth ?? SKFontStyleWidth.Normal);
        var fontStyle = ToSKFontStyleSlant(typeface?.FontSlant ?? SKFontStyleSlant.Upright);
        var style = new SkiaSharp.SKFontStyle(fontWeight, fontWidth, fontStyle);
        var cacheKey = new TypefaceKey(fontFamily, fontWeight, fontWidth, fontStyle);

        EnsureTypefaceProviderCaches();

        if (_typefaceCache.TryGetValue(cacheKey, out var cached))
        {
            if (cached.Typeface.Handle != IntPtr.Zero)
            {
                return cached;
            }

            _typefaceCache.TryRemove(cacheKey, out _);
        }

        const bool browserCompatibleFontFallback = true;
        foreach (var candidate in EnumerateFontFamilyCandidates(fontFamily, browserCompatibleFontFallback))
        {
            foreach (var typefaceProvider in EnumerateEffectiveTypefaceProviders())
            {
                var providerTypeface = ResolveProviderTypeface(typefaceProvider, candidate, fontWeight, fontWidth, fontStyle);
                if (providerTypeface is { } && providerTypeface.Handle != IntPtr.Zero)
                {
                    return CacheTypefaceResolution(cacheKey, providerTypeface, ShouldSuppressSyntheticBold(typefaceProvider, candidate, providerTypeface));
                }
            }

            var resolved = ResolveTypeface(candidate, style);
            if (resolved is { } &&
                resolved.Handle != IntPtr.Zero &&
                IsAcceptableResolvedFamily(candidate, resolved))
            {
                return CacheTypefaceResolution(cacheKey, resolved, suppressSyntheticBold: false);
            }
        }

        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            foreach (var candidate in EnumerateFontFamilyCandidates("serif", browserCompatibleFontFallback))
            {
                foreach (var typefaceProvider in EnumerateEffectiveTypefaceProviders())
                {
                    var providerTypeface = ResolveProviderTypeface(typefaceProvider, candidate, fontWeight, fontWidth, fontStyle);
                    if (providerTypeface is { } && providerTypeface.Handle != IntPtr.Zero)
                    {
                        return CacheTypefaceResolution(cacheKey, providerTypeface, ShouldSuppressSyntheticBold(typefaceProvider, candidate, providerTypeface));
                    }
                }

                var resolved = ResolveTypeface(candidate, style);
                if (resolved is { } &&
                    resolved.Handle != IntPtr.Zero &&
                    IsAcceptableResolvedFamily(candidate, resolved))
                {
                    return CacheTypefaceResolution(cacheKey, resolved, suppressSyntheticBold: false);
                }
            }
        }

        foreach (var typefaceProvider in EnumerateEffectiveTypefaceProviders())
        {
            var providerTypeface = ResolveProviderTypeface(typefaceProvider, SkiaSharp.SKTypeface.Default.FamilyName, fontWeight, fontWidth, fontStyle);
            if (providerTypeface is { } && providerTypeface.Handle != IntPtr.Zero)
            {
                return CacheTypefaceResolution(cacheKey, providerTypeface, ShouldSuppressSyntheticBold(typefaceProvider, SkiaSharp.SKTypeface.Default.FamilyName, providerTypeface));
            }
        }

        var defaultTypeface = SkiaSharp.SKTypeface.FromFamilyName(null, fontWeight, fontWidth, fontStyle);
        if (defaultTypeface is { } && defaultTypeface.Handle == IntPtr.Zero)
        {
            defaultTypeface = null;
        }

        var fallback = defaultTypeface ?? SkiaSharp.SKTypeface.Default;
        return CacheTypefaceResolution(cacheKey, fallback, suppressSyntheticBold: false);
    }

    private static bool IsAcceptableResolvedFamily(string candidate, SkiaSharp.SKTypeface resolved)
    {
        return s_genericFontFamilyMap.ContainsKey(candidate) ||
               s_browserCompatibleGenericFontFamilyMap.ContainsKey(candidate) ||
               string.Equals(resolved.FamilyName, candidate, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool HasExplicitTypeface(SKTypeface? typeface)
    {
        return !string.IsNullOrWhiteSpace(typeface?.FamilyName);
    }

    private TypefaceResolution? ResolveExplicitTypeface(SKTypeface? typeface)
    {
        return HasExplicitTypeface(typeface) ? ResolveSKTypeface(typeface) : null;
    }

    private TypefaceResolution? ResolvePaintTypeface(SKPaint paint)
    {
        return ResolveExplicitTypeface(paint.Typeface);
    }

    internal SkiaSharp.SKPaint? ToSKTextPaint(SKPaint? paint)
    {
        var skPaint = paint is null
            ? null
            : CreateRenderPaint(paint);
        if (paint is null || skPaint is null)
        {
            return skPaint;
        }

        var typefaceResolution = ResolveSKTypeface(paint.Typeface);
        skPaint.Typeface = typefaceResolution.Typeface;
        skPaint.FakeBoldText = false;
        ApplyTypefaceAdjustments(paint, skPaint, typefaceResolution.SuppressSyntheticBold);
        return skPaint;
    }

    private TypefaceResolution CacheTypefaceResolution(TypefaceKey cacheKey, SkiaSharp.SKTypeface typeface, bool suppressSyntheticBold)
    {
        var resolution = new TypefaceResolution(typeface, suppressSyntheticBold);
        _typefaceCache.TryAdd(cacheKey, resolution);
        TrimTypefaceCachesIfNeeded();
        return resolution;
    }

    private static bool ShouldSuppressSyntheticBold(ITypefaceProvider typefaceProvider, string candidate, SkiaSharp.SKTypeface providerTypeface)
    {
        if (typefaceProvider is FontManagerTypefaceProvider or DefaultTypefaceProvider or DocumentFontTypefaceProvider)
        {
            return false;
        }

        return !string.Equals(providerTypeface.FamilyName, candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGenericFontFamilyName(string candidate)
    {
        return candidate.Equals("serif", StringComparison.OrdinalIgnoreCase) ||
               candidate.Equals("sans-serif", StringComparison.OrdinalIgnoreCase) ||
               candidate.Equals("monospace", StringComparison.OrdinalIgnoreCase) ||
               candidate.Equals("cursive", StringComparison.OrdinalIgnoreCase) ||
               candidate.Equals("fantasy", StringComparison.OrdinalIgnoreCase);
    }

    public SkiaSharp.SKColor ToSKColor(SKColor color)
    {
        return new(color.Red, color.Green, color.Blue, color.Alpha);
    }

    public SkiaSharp.SKColor[] ToSKColors(SKColor[] colors)
    {
        var skColors = new SkiaSharp.SKColor[colors.Length];

        for (var i = 0; i < colors.Length; i++)
        {
            skColors[i] = ToSKColor(colors[i]);
        }

        return skColors;
    }

    public SkiaSharp.SKColorF ToSKColor(SKColorF color)
    {
        return new(color.Red, color.Green, color.Blue, color.Alpha);
    }

    public SkiaSharp.SKColorF[] ToSKColors(SKColorF[] colors)
    {
        var skColors = new SkiaSharp.SKColorF[colors.Length];

        for (var i = 0; i < colors.Length; i++)
        {
            skColors[i] = ToSKColor(colors[i]);
        }

        return skColors;
    }

    public SkiaSharp.SKShaderTileMode ToSKShaderTileMode(SKShaderTileMode shaderTileMode)
    {
        return shaderTileMode switch
        {
            SKShaderTileMode.Clamp => SkiaSharp.SKShaderTileMode.Clamp,
            SKShaderTileMode.Repeat => SkiaSharp.SKShaderTileMode.Repeat,
            SKShaderTileMode.Mirror => SkiaSharp.SKShaderTileMode.Mirror,
            SKShaderTileMode.Decal => SkiaSharp.SKShaderTileMode.Decal,
            _ => SkiaSharp.SKShaderTileMode.Clamp
        };
    }

    private static float[]? GetGradientColorPositions(float[]? colorPos)
    {
        if (colorPos is null)
        {
            return null;
        }

        for (var i = 0; i < colorPos.Length; i++)
        {
            var value = colorPos[i];
            if (float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
            {
                return null;
            }
        }

        return colorPos;
    }

    public SkiaSharp.SKShader? ToSKShader(SKShader? shader)
    {
        switch (shader)
        {
            case ColorShader colorShader:
                {
                    return SkiaSharp.SKShader.CreateColor(
                        ToSKColor(colorShader.Color),
                        colorShader.ColorSpace == SKColorSpace.Srgb
                            ? Settings.Srgb
                            : Settings.SrgbLinear);
                }
            case LinearGradientShader linearGradientShader:
                {
                    if (linearGradientShader.Colors is null)
                    {
                        return null;
                    }

                    var colorPos = GetGradientColorPositions(linearGradientShader.ColorPos);
                    if (linearGradientShader.LocalMatrix is { })
                    {
                        return SkiaSharp.SKShader.CreateLinearGradient(
                            ToSKPoint(linearGradientShader.Start),
                            ToSKPoint(linearGradientShader.End),
                            ToSKColors(linearGradientShader.Colors),
                            linearGradientShader.ColorSpace == SKColorSpace.Srgb
                                ? Settings.Srgb
                                : Settings.SrgbLinear,
                            colorPos,
                            ToSKShaderTileMode(linearGradientShader.Mode),
                            ToSKMatrix(linearGradientShader.LocalMatrix.Value));
                    }

                    return SkiaSharp.SKShader.CreateLinearGradient(
                        ToSKPoint(linearGradientShader.Start),
                        ToSKPoint(linearGradientShader.End),
                        ToSKColors(linearGradientShader.Colors),
                        linearGradientShader.ColorSpace == SKColorSpace.Srgb
                            ? Settings.Srgb
                            : Settings.SrgbLinear,
                        colorPos,
                        ToSKShaderTileMode(linearGradientShader.Mode));
                }
            case RadialGradientShader radialGradientShader:
                {
                    if (radialGradientShader.Colors is null)
                    {
                        return null;
                    }

                    var colorPos = GetGradientColorPositions(radialGradientShader.ColorPos);
                    if (radialGradientShader.LocalMatrix is { })
                    {
                        return SkiaSharp.SKShader.CreateRadialGradient(
                            ToSKPoint(radialGradientShader.Center),
                            radialGradientShader.Radius,
                            ToSKColors(radialGradientShader.Colors),
                            radialGradientShader.ColorSpace == SKColorSpace.Srgb
                                ? Settings.Srgb
                                : Settings.SrgbLinear,
                            colorPos,
                            ToSKShaderTileMode(radialGradientShader.Mode),
                            ToSKMatrix(radialGradientShader.LocalMatrix.Value));
                    }

                    return SkiaSharp.SKShader.CreateRadialGradient(
                        ToSKPoint(radialGradientShader.Center),
                        radialGradientShader.Radius,
                        ToSKColors(radialGradientShader.Colors),
                        radialGradientShader.ColorSpace == SKColorSpace.Srgb
                            ? Settings.Srgb
                            : Settings.SrgbLinear,
                        colorPos,
                        ToSKShaderTileMode(radialGradientShader.Mode));
                }
            case TwoPointConicalGradientShader twoPointConicalGradientShader:
                {
                    if (twoPointConicalGradientShader.Colors is null)
                    {
                        return null;
                    }

                    var colorPos = GetGradientColorPositions(twoPointConicalGradientShader.ColorPos);
                    if (twoPointConicalGradientShader.LocalMatrix is { })
                    {
                        return SkiaSharp.SKShader.CreateTwoPointConicalGradient(
                            ToSKPoint(twoPointConicalGradientShader.Start),
                            twoPointConicalGradientShader.StartRadius,
                            ToSKPoint(twoPointConicalGradientShader.End),
                            twoPointConicalGradientShader.EndRadius,
                            ToSKColors(twoPointConicalGradientShader.Colors),
                            twoPointConicalGradientShader.ColorSpace == SKColorSpace.Srgb
                                ? Settings.Srgb
                                : Settings.SrgbLinear,
                            colorPos,
                            ToSKShaderTileMode(twoPointConicalGradientShader.Mode),
                            ToSKMatrix(twoPointConicalGradientShader.LocalMatrix.Value));
                    }

                    return SkiaSharp.SKShader.CreateTwoPointConicalGradient(
                        ToSKPoint(twoPointConicalGradientShader.Start),
                        twoPointConicalGradientShader.StartRadius,
                        ToSKPoint(twoPointConicalGradientShader.End),
                        twoPointConicalGradientShader.EndRadius,
                        ToSKColors(twoPointConicalGradientShader.Colors),
                        twoPointConicalGradientShader.ColorSpace == SKColorSpace.Srgb
                            ? Settings.Srgb
                            : Settings.SrgbLinear,
                        colorPos,
                        ToSKShaderTileMode(twoPointConicalGradientShader.Mode));
                }
            case PictureShader pictureShader:
                {
                    if (pictureShader.Src is null)
                    {
                        return null;
                    }

                    return SkiaSharp.SKShader.CreatePicture(
                        GetRenderPicture(pictureShader.Src),
                        SkiaSharp.SKShaderTileMode.Repeat,
                        SkiaSharp.SKShaderTileMode.Repeat,
                        ToSKMatrix(pictureShader.LocalMatrix),
                        ToSKRect(pictureShader.Tile));
                }
            case PerlinNoiseFractalNoiseShader perlinNoiseFractalNoiseShader:
                {
                    return SkiaSharp.SKShader.CreatePerlinNoiseFractalNoise(
                        perlinNoiseFractalNoiseShader.BaseFrequencyX,
                        perlinNoiseFractalNoiseShader.BaseFrequencyY,
                        perlinNoiseFractalNoiseShader.NumOctaves,
                        perlinNoiseFractalNoiseShader.Seed,
                        ToSKPointI(perlinNoiseFractalNoiseShader.TileSize));
                }
            case PerlinNoiseTurbulenceShader perlinNoiseTurbulenceShader:
                {
                    return SkiaSharp.SKShader.CreatePerlinNoiseTurbulence(
                        perlinNoiseTurbulenceShader.BaseFrequencyX,
                        perlinNoiseTurbulenceShader.BaseFrequencyY,
                        perlinNoiseTurbulenceShader.NumOctaves,
                        perlinNoiseTurbulenceShader.Seed,
                        ToSKPointI(perlinNoiseTurbulenceShader.TileSize));
                }
            default:
                return null;
        }
    }

    public SkiaSharp.SKColorFilter? ToSKColorFilter(SKColorFilter? colorFilter)
    {
        switch (colorFilter)
        {
            case BlendModeColorFilter blendModeColorFilter:
                {
                    return SkiaSharp.SKColorFilter.CreateBlendMode(
                        ToSKColor(blendModeColorFilter.Color),
                        ToSKBlendMode(blendModeColorFilter.Mode));
                }
            case ColorMatrixColorFilter colorMatrixColorFilter:
                {
                    if (colorMatrixColorFilter.Matrix is null)
                    {
                        return null;
                    }
                    return SkiaSharp.SKColorFilter.CreateColorMatrix(colorMatrixColorFilter.Matrix);
                }
            case LumaColorColorFilter _:
                {
                    return SkiaSharp.SKColorFilter.CreateLumaColor();
                }
            case TableColorFilter tableColorFilter:
                {
                    if (tableColorFilter.TableA is null
                        || tableColorFilter.TableR is null
                        || tableColorFilter.TableG is null
                        || tableColorFilter.TableB is null)
                        return null;
                    {
                        return SkiaSharp.SKColorFilter.CreateTable(
                            tableColorFilter.TableA,
                            tableColorFilter.TableR,
                            tableColorFilter.TableG,
                            tableColorFilter.TableB);
                    }
                }
            default:
                {
                    return null;
                }
        }
    }

    public SkiaSharp.SKColorChannel ToSKColorChannel(SKColorChannel colorChannel)
    {
        return colorChannel switch
        {
            SKColorChannel.R => SkiaSharp.SKColorChannel.R,
            SKColorChannel.G => SkiaSharp.SKColorChannel.G,
            SKColorChannel.B => SkiaSharp.SKColorChannel.B,
            SKColorChannel.A => SkiaSharp.SKColorChannel.A,
            _ => SkiaSharp.SKColorChannel.R
        };
    }

    private SkiaSharp.SKImageFilter? CreatePaint(SKPaint skPaint, SKRect? skCropRect = null)
    {
        if (skPaint.Shader is null && skPaint.Color is null)
        {
            return null;
        }

        var skShader = skPaint.Shader is null
            ? SkiaSharp.SKShader.CreateColor(ToSKColor(skPaint.Color!.Value), Settings.Srgb)
            : GetRenderShader(skPaint.Shader);

        if (skShader is null)
        {
            return null;
        }

        if (skCropRect == null)
        {
            var skImageFilter = SkiaSharp.SKImageFilter.CreateShader(skShader, skPaint.IsDither);
            return skImageFilter;
        }
        else
        {
            var skImageFilter = SkiaSharp.SKImageFilter.CreateShader(skShader, skPaint.IsDither, ToSKRect(skCropRect.Value));
            return skImageFilter;
        }
    }

    public SkiaSharp.SKImageFilter? ToSKImageFilter(SKImageFilter? imageFilter)
    {
        switch (imageFilter)
        {
            case ArithmeticImageFilter arithmeticImageFilter:
                {
                    if (arithmeticImageFilter.Background is null)
                    {
                        return null;
                    }

                    return arithmeticImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateArithmetic(
                            arithmeticImageFilter.K1,
                            arithmeticImageFilter.K2,
                            arithmeticImageFilter.K3,
                            arithmeticImageFilter.K4,
                            arithmeticImageFilter.EforcePMColor,
                            GetRenderImageFilter(arithmeticImageFilter.Background),
                            GetRenderImageFilter(arithmeticImageFilter.Foreground),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateArithmetic(
                            arithmeticImageFilter.K1,
                            arithmeticImageFilter.K2,
                            arithmeticImageFilter.K3,
                            arithmeticImageFilter.K4,
                            arithmeticImageFilter.EforcePMColor,
                            GetRenderImageFilter(arithmeticImageFilter.Background),
                            GetRenderImageFilter(arithmeticImageFilter.Foreground));
                }
            case BlendModeImageFilter blendModeImageFilter:
                {
                    if (blendModeImageFilter.Background is null)
                    {
                        return null;
                    }

                    return blendModeImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateBlendMode(
                            ToSKBlendMode(blendModeImageFilter.Mode),
                            GetRenderImageFilter(blendModeImageFilter.Background),
                            GetRenderImageFilter(blendModeImageFilter.Foreground),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateBlendMode(
                            ToSKBlendMode(blendModeImageFilter.Mode),
                            GetRenderImageFilter(blendModeImageFilter.Background),
                            GetRenderImageFilter(blendModeImageFilter.Foreground));
                }
            case BlurImageFilter blurImageFilter:
                {
                    return blurImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateBlur(
                            blurImageFilter.SigmaX,
                            blurImageFilter.SigmaY,
                            SkiaSharp.SKShaderTileMode.Decal,
                            GetRenderImageFilter(blurImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateBlur(
                            blurImageFilter.SigmaX,
                            blurImageFilter.SigmaY,
                            SkiaSharp.SKShaderTileMode.Decal,
                            GetRenderImageFilter(blurImageFilter.Input));
                }
            case ColorFilterImageFilter colorFilterImageFilter:
                {
                    if (colorFilterImageFilter.ColorFilter is null ||
                        GetRenderColorFilter(colorFilterImageFilter.ColorFilter) is not { } colorFilter)
                    {
                        return null;
                    }

                    return colorFilterImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateColorFilter(
                            colorFilter,
                            GetRenderImageFilter(colorFilterImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateColorFilter(
                            colorFilter,
                            GetRenderImageFilter(colorFilterImageFilter.Input));
                }
            case DilateImageFilter dilateImageFilter:
                {
                    return dilateImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateDilate(
                            dilateImageFilter.RadiusX,
                            dilateImageFilter.RadiusY,
                            GetRenderImageFilter(dilateImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateDilate(
                            dilateImageFilter.RadiusX,
                            dilateImageFilter.RadiusY,
                            GetRenderImageFilter(dilateImageFilter.Input));
                }
            case DisplacementMapEffectImageFilter displacementMapEffectImageFilter:
                {
                    if (displacementMapEffectImageFilter.Displacement is null ||
                        GetRenderImageFilter(displacementMapEffectImageFilter.Displacement) is not { } displacement)
                    {
                        return null;
                    }

                    return displacementMapEffectImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateDisplacementMapEffect(
                            ToSKColorChannel(displacementMapEffectImageFilter.XChannelSelector),
                            ToSKColorChannel(displacementMapEffectImageFilter.YChannelSelector),
                            displacementMapEffectImageFilter.Scale,
                            displacement,
                            GetRenderImageFilter(displacementMapEffectImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateDisplacementMapEffect(
                            ToSKColorChannel(displacementMapEffectImageFilter.XChannelSelector),
                            ToSKColorChannel(displacementMapEffectImageFilter.YChannelSelector),
                            displacementMapEffectImageFilter.Scale,
                            displacement,
                            GetRenderImageFilter(displacementMapEffectImageFilter.Input));
                }
            case DistantLitDiffuseImageFilter distantLitDiffuseImageFilter:
                {
                    return distantLitDiffuseImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateDistantLitDiffuse(
                            ToSKPoint3(distantLitDiffuseImageFilter.Direction),
                            ToSKColor(distantLitDiffuseImageFilter.LightColor),
                            distantLitDiffuseImageFilter.SurfaceScale,
                            distantLitDiffuseImageFilter.Kd,
                            GetRenderImageFilter(distantLitDiffuseImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateDistantLitDiffuse(
                            ToSKPoint3(distantLitDiffuseImageFilter.Direction),
                            ToSKColor(distantLitDiffuseImageFilter.LightColor),
                            distantLitDiffuseImageFilter.SurfaceScale,
                            distantLitDiffuseImageFilter.Kd,
                            GetRenderImageFilter(distantLitDiffuseImageFilter.Input));
                }
            case DistantLitSpecularImageFilter distantLitSpecularImageFilter:
                {
                    return distantLitSpecularImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateDistantLitSpecular(
                            ToSKPoint3(distantLitSpecularImageFilter.Direction),
                            ToSKColor(distantLitSpecularImageFilter.LightColor),
                            distantLitSpecularImageFilter.SurfaceScale,
                            distantLitSpecularImageFilter.Ks,
                            distantLitSpecularImageFilter.Shininess,
                            GetRenderImageFilter(distantLitSpecularImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateDistantLitSpecular(
                            ToSKPoint3(distantLitSpecularImageFilter.Direction),
                            ToSKColor(distantLitSpecularImageFilter.LightColor),
                            distantLitSpecularImageFilter.SurfaceScale,
                            distantLitSpecularImageFilter.Ks,
                            distantLitSpecularImageFilter.Shininess,
                            GetRenderImageFilter(distantLitSpecularImageFilter.Input));
                }
            case ErodeImageFilter erodeImageFilter:
                {
                    return erodeImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateErode(
                            erodeImageFilter.RadiusX,
                            erodeImageFilter.RadiusY,
                            GetRenderImageFilter(erodeImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateErode(
                            erodeImageFilter.RadiusX,
                            erodeImageFilter.RadiusY,
                            GetRenderImageFilter(erodeImageFilter.Input));
                }
            case ImageImageFilter imageImageFilter:
                {
                    if (imageImageFilter.Image is null ||
                        GetRenderImage(imageImageFilter.Image) is not { } image)
                    {
                        return null;
                    }

                    return SkiaSharp.SKImageFilter.CreateImage(
                        image,
                        ToSKRect(imageImageFilter.Src),
                        ToSKRect(imageImageFilter.Dst),
                        ToSKSamplingOptions(imageImageFilter.FilterQuality));
                }
            case MatrixConvolutionImageFilter matrixConvolutionImageFilter:
                {
                    if (matrixConvolutionImageFilter.Kernel is null)
                    {
                        return null;
                    }

                    return matrixConvolutionImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateMatrixConvolution(
                            ToSKSizeI(matrixConvolutionImageFilter.KernelSize),
                            matrixConvolutionImageFilter.Kernel,
                            matrixConvolutionImageFilter.Gain,
                            matrixConvolutionImageFilter.Bias,
                            ToSKPointI(matrixConvolutionImageFilter.KernelOffset),
                            ToSKShaderTileMode(matrixConvolutionImageFilter.TileMode),
                            matrixConvolutionImageFilter.ConvolveAlpha,
                            GetRenderImageFilter(matrixConvolutionImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateMatrixConvolution(
                            ToSKSizeI(matrixConvolutionImageFilter.KernelSize),
                            matrixConvolutionImageFilter.Kernel,
                            matrixConvolutionImageFilter.Gain,
                            matrixConvolutionImageFilter.Bias,
                            ToSKPointI(matrixConvolutionImageFilter.KernelOffset),
                            ToSKShaderTileMode(matrixConvolutionImageFilter.TileMode),
                            matrixConvolutionImageFilter.ConvolveAlpha,
                            GetRenderImageFilter(matrixConvolutionImageFilter.Input));
                }
            case MergeImageFilter mergeImageFilter:
                {
                    if (mergeImageFilter.Filters is null)
                    {
                        return null;
                    }

                    return mergeImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateMerge(
                            ToSKImageFilters(mergeImageFilter.Filters),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateMerge(
                            ToSKImageFilters(mergeImageFilter.Filters));
                }
            case OffsetImageFilter offsetImageFilter:
                {
                    return offsetImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateOffset(
                            offsetImageFilter.Dx,
                            offsetImageFilter.Dy,
                            GetRenderImageFilter(offsetImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateOffset(
                            offsetImageFilter.Dx,
                            offsetImageFilter.Dy,
                            GetRenderImageFilter(offsetImageFilter.Input));
                }
            case PaintImageFilter paintImageFilter:
                {
                    if (paintImageFilter.Paint is null)
                    {
                        return null;
                    }

                    return paintImageFilter.Clip is { } clip
                        ? CreatePaint(paintImageFilter.Paint, clip)
                        : CreatePaint(paintImageFilter.Paint);
                }
            case ShaderImageFilter shaderImageFilter:
                {
                    if (shaderImageFilter.Shader is null ||
                        GetRenderShader(shaderImageFilter.Shader) is not { } shader)
                    {
                        return null;
                    }

                    return shaderImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateShader(
                            shader,
                            shaderImageFilter.Dither,
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateShader(
                            shader,
                            shaderImageFilter.Dither);
                }
            case PictureImageFilter pictureImageFilter:
                {
                    if (pictureImageFilter.Picture is null ||
                        GetRenderPicture(pictureImageFilter.Picture) is not { } picture)
                    {
                        return null;
                    }

                    return SkiaSharp.SKImageFilter.CreatePicture(
                        picture,
                        ToSKRect(pictureImageFilter.Clip ?? pictureImageFilter.Picture.CullRect));
                }
            case PointLitDiffuseImageFilter pointLitDiffuseImageFilter:
                {
                    return pointLitDiffuseImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreatePointLitDiffuse(
                            ToSKPoint3(pointLitDiffuseImageFilter.Location),
                            ToSKColor(pointLitDiffuseImageFilter.LightColor),
                            pointLitDiffuseImageFilter.SurfaceScale,
                            pointLitDiffuseImageFilter.Kd,
                            GetRenderImageFilter(pointLitDiffuseImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreatePointLitDiffuse(
                            ToSKPoint3(pointLitDiffuseImageFilter.Location),
                            ToSKColor(pointLitDiffuseImageFilter.LightColor),
                            pointLitDiffuseImageFilter.SurfaceScale,
                            pointLitDiffuseImageFilter.Kd,
                            GetRenderImageFilter(pointLitDiffuseImageFilter.Input));
                }
            case PointLitSpecularImageFilter pointLitSpecularImageFilter:
                {
                    return pointLitSpecularImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreatePointLitSpecular(
                            ToSKPoint3(pointLitSpecularImageFilter.Location),
                            ToSKColor(pointLitSpecularImageFilter.LightColor),
                            pointLitSpecularImageFilter.SurfaceScale,
                            pointLitSpecularImageFilter.Ks,
                            pointLitSpecularImageFilter.Shininess,
                            GetRenderImageFilter(pointLitSpecularImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreatePointLitSpecular(
                            ToSKPoint3(pointLitSpecularImageFilter.Location),
                            ToSKColor(pointLitSpecularImageFilter.LightColor),
                            pointLitSpecularImageFilter.SurfaceScale,
                            pointLitSpecularImageFilter.Ks,
                            pointLitSpecularImageFilter.Shininess,
                            GetRenderImageFilter(pointLitSpecularImageFilter.Input));
                }
            case SpotLitDiffuseImageFilter spotLitDiffuseImageFilter:
                {
                    return spotLitDiffuseImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateSpotLitDiffuse(
                            ToSKPoint3(spotLitDiffuseImageFilter.Location),
                            ToSKPoint3(spotLitDiffuseImageFilter.Target),
                            spotLitDiffuseImageFilter.SpecularExponent,
                            spotLitDiffuseImageFilter.CutoffAngle,
                            ToSKColor(spotLitDiffuseImageFilter.LightColor),
                            spotLitDiffuseImageFilter.SurfaceScale,
                            spotLitDiffuseImageFilter.Kd,
                            GetRenderImageFilter(spotLitDiffuseImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateSpotLitDiffuse(
                            ToSKPoint3(spotLitDiffuseImageFilter.Location),
                            ToSKPoint3(spotLitDiffuseImageFilter.Target),
                            spotLitDiffuseImageFilter.SpecularExponent,
                            spotLitDiffuseImageFilter.CutoffAngle,
                            ToSKColor(spotLitDiffuseImageFilter.LightColor),
                            spotLitDiffuseImageFilter.SurfaceScale,
                            spotLitDiffuseImageFilter.Kd,
                            GetRenderImageFilter(spotLitDiffuseImageFilter.Input));
                }
            case SpotLitSpecularImageFilter spotLitSpecularImageFilter:
                {
                    return spotLitSpecularImageFilter.Clip is { } clip
                        ? SkiaSharp.SKImageFilter.CreateSpotLitSpecular(
                            ToSKPoint3(spotLitSpecularImageFilter.Location),
                            ToSKPoint3(spotLitSpecularImageFilter.Target),
                            spotLitSpecularImageFilter.SpecularExponent,
                            spotLitSpecularImageFilter.CutoffAngle,
                            ToSKColor(spotLitSpecularImageFilter.LightColor),
                            spotLitSpecularImageFilter.SurfaceScale,
                            spotLitSpecularImageFilter.Ks,
                            spotLitSpecularImageFilter.Shininess,
                            GetRenderImageFilter(spotLitSpecularImageFilter.Input),
                            ToSKRect(clip))
                        : SkiaSharp.SKImageFilter.CreateSpotLitSpecular(
                            ToSKPoint3(spotLitSpecularImageFilter.Location),
                            ToSKPoint3(spotLitSpecularImageFilter.Target),
                            spotLitSpecularImageFilter.SpecularExponent,
                            spotLitSpecularImageFilter.CutoffAngle,
                            ToSKColor(spotLitSpecularImageFilter.LightColor),
                            spotLitSpecularImageFilter.SurfaceScale,
                            spotLitSpecularImageFilter.Ks,
                            spotLitSpecularImageFilter.Shininess,
                            GetRenderImageFilter(spotLitSpecularImageFilter.Input));
                }
            case TileImageFilter tileImageFilter:
                {
                    return SkiaSharp.SKImageFilter.CreateTile(
                        ToSKRect(tileImageFilter.Src),
                        ToSKRect(tileImageFilter.Dst),
                        GetRenderImageFilter(tileImageFilter.Input));
                }
            default:
                {
                    return null;
                }
        }
    }

    public SkiaSharp.SKImageFilter[]? ToSKImageFilters(SKImageFilter[]? imageFilters)
    {
        if (imageFilters is null)
        {
            return null;
        }

        var skImageFilters = new SkiaSharp.SKImageFilter[imageFilters.Length];

        for (var i = 0; i < imageFilters.Length; i++)
        {
            var imageFilter = imageFilters[i];
            var skImageFilter = GetRenderImageFilter(imageFilter);
            if (skImageFilter is { })
            {
                skImageFilters[i] = skImageFilter;
            }
        }

        return skImageFilters;
    }

    public SkiaSharp.SKPathEffect? ToSKPathEffect(SKPathEffect? pathEffect)
    {
        switch (pathEffect)
        {
            case DashPathEffect dashPathEffect:
                {
                    return SkiaSharp.SKPathEffect.CreateDash(
                        dashPathEffect.Intervals,
                        dashPathEffect.Phase);
                }
            default:
                {
                    return null;
                }
        }
    }

    public SkiaSharp.SKBlendMode ToSKBlendMode(SKBlendMode blendMode)
    {
        return blendMode switch
        {
            SKBlendMode.Clear => SkiaSharp.SKBlendMode.Clear,
            SKBlendMode.Src => SkiaSharp.SKBlendMode.Src,
            SKBlendMode.Dst => SkiaSharp.SKBlendMode.Dst,
            SKBlendMode.SrcOver => SkiaSharp.SKBlendMode.SrcOver,
            SKBlendMode.DstOver => SkiaSharp.SKBlendMode.DstOver,
            SKBlendMode.SrcIn => SkiaSharp.SKBlendMode.SrcIn,
            SKBlendMode.DstIn => SkiaSharp.SKBlendMode.DstIn,
            SKBlendMode.SrcOut => SkiaSharp.SKBlendMode.SrcOut,
            SKBlendMode.DstOut => SkiaSharp.SKBlendMode.DstOut,
            SKBlendMode.SrcATop => SkiaSharp.SKBlendMode.SrcATop,
            SKBlendMode.DstATop => SkiaSharp.SKBlendMode.DstATop,
            SKBlendMode.Xor => SkiaSharp.SKBlendMode.Xor,
            SKBlendMode.Plus => SkiaSharp.SKBlendMode.Plus,
            SKBlendMode.Modulate => SkiaSharp.SKBlendMode.Modulate,
            SKBlendMode.Screen => SkiaSharp.SKBlendMode.Screen,
            SKBlendMode.Overlay => SkiaSharp.SKBlendMode.Overlay,
            SKBlendMode.Darken => SkiaSharp.SKBlendMode.Darken,
            SKBlendMode.Lighten => SkiaSharp.SKBlendMode.Lighten,
            SKBlendMode.ColorDodge => SkiaSharp.SKBlendMode.ColorDodge,
            SKBlendMode.ColorBurn => SkiaSharp.SKBlendMode.ColorBurn,
            SKBlendMode.HardLight => SkiaSharp.SKBlendMode.HardLight,
            SKBlendMode.SoftLight => SkiaSharp.SKBlendMode.SoftLight,
            SKBlendMode.Difference => SkiaSharp.SKBlendMode.Difference,
            SKBlendMode.Exclusion => SkiaSharp.SKBlendMode.Exclusion,
            SKBlendMode.Multiply => SkiaSharp.SKBlendMode.Multiply,
            SKBlendMode.Hue => SkiaSharp.SKBlendMode.Hue,
            SKBlendMode.Saturation => SkiaSharp.SKBlendMode.Saturation,
            SKBlendMode.Color => SkiaSharp.SKBlendMode.Color,
            SKBlendMode.Luminosity => SkiaSharp.SKBlendMode.Luminosity,
            _ => SkiaSharp.SKBlendMode.Clear
        };
    }

    public SkiaSharp.SKSamplingOptions ToSKSamplingOptions(SKFilterQuality filterQuality)
    {
        return filterQuality switch
        {
            SKFilterQuality.None => new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Nearest, SkiaSharp.SKMipmapMode.None),
            SKFilterQuality.Low => new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear, SkiaSharp.SKMipmapMode.None),
            SKFilterQuality.Medium => new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear, SkiaSharp.SKMipmapMode.Linear),
            SKFilterQuality.High => new SkiaSharp.SKSamplingOptions(SkiaSharp.SKCubicResampler.Mitchell),
            _ => SkiaSharp.SKSamplingOptions.Default
        };
    }

    private static SkiaSharp.SKSamplingOptions ToSKSamplingOptions(SKSamplingOptions samplingOptions)
    {
        if (samplingOptions.UseCubic)
        {
            return new SkiaSharp.SKSamplingOptions(
                new SkiaSharp.SKCubicResampler(
                    samplingOptions.Cubic.B,
                    samplingOptions.Cubic.C));
        }

        return new SkiaSharp.SKSamplingOptions(
            (SkiaSharp.SKFilterMode)(int)samplingOptions.Filter,
            (SkiaSharp.SKMipmapMode)(int)samplingOptions.Mipmap);
    }

    private static SkiaSharp.SKPaint CreateTextRenderPaint(
        SkiaSharp.SKPaint paint,
        SkiaSharp.SKTextAlign textAlign,
        SkiaSharp.SKFont font,
        bool applyFont)
    {
        var textPaint = paint.Clone();
        textPaint.TextAlign = textAlign;

        if (applyFont)
        {
            ApplyFontToPaint(font, textPaint);
        }

        return textPaint;
    }

    private static void ApplyFontToPaint(SkiaSharp.SKFont font, SkiaSharp.SKPaint paint)
    {
        paint.Typeface = font.Typeface;
        paint.TextSize = font.Size;
        paint.TextScaleX = font.ScaleX;
        paint.TextSkewX = font.SkewX;
        paint.SubpixelText = font.Subpixel;
        paint.FakeBoldText = font.Embolden;
        paint.LcdRenderText = font.Edging == SkiaSharp.SKFontEdging.SubpixelAntialias;
        paint.IsAntialias = font.Edging != SkiaSharp.SKFontEdging.Alias;
    }

    private static void DisposeIfCloned(SkiaSharp.SKPaint paint, SkiaSharp.SKPaint sourcePaint)
    {
        if (!ReferenceEquals(paint, sourcePaint))
        {
            paint.Dispose();
        }
    }

    public SkiaSharp.SKFont ToSKFont(SKPaint paint)
    {
        var typefaceResolution = ResolveSKTypeface(paint.Typeface);
        var skFont = new SkiaSharp.SKFont(typefaceResolution.Typeface, paint.TextSize)
        {
            Edging = ToSKFontEdging(paint),
            Subpixel = paint.SubpixelText
        };

        ApplyTypefaceAdjustments(paint, skFont, typefaceResolution.SuppressSyntheticBold);
        return skFont;
    }

    public SkiaSharp.SKFont? ToSKFont(SKFont? font)
    {
        if (font is null)
        {
            return null;
        }

        var typefaceResolution = ResolveSKTypeface(font.Typeface);
        var skFont = new SkiaSharp.SKFont(typefaceResolution.Typeface, font.Size, font.ScaleX, font.SkewX)
        {
            Edging = ToSKFontEdging(font.Edging),
            Subpixel = font.Subpixel,
            Embolden = font.Embolden
        };

        ApplyTypefaceAdjustments(font, skFont, typefaceResolution.SuppressSyntheticBold);
        return skFont;
    }

    public SkiaSharp.SKPaint? ToSKPaint(SKPaint? paint)
    {
        if (paint is null)
        {
            return null;
        }

        var style = ToSKPaintStyle(paint.Style);
        var strokeCap = ToSKStrokeCap(paint.StrokeCap);
        var strokeJoin = ToSKStrokeJoin(paint.StrokeJoin);
        var textAlign = ToSKTextAlign(paint.TextAlign);
        var typefaceResolution = ResolvePaintTypeface(paint);
        var typeface = typefaceResolution?.Typeface;
        var textEncoding = ToSKTextEncoding(paint.TextEncoding);
        var color = paint.Color is null
            ? SkiaSharp.SKColor.Empty :
            ToSKColor(paint.Color.Value);
        var shader = ToSKShader(paint.Shader);
        var colorFilter = ToSKColorFilter(paint.ColorFilter);
        var imageFilter = ToSKImageFilter(paint.ImageFilter);
        var pathEffect = ToSKPathEffect(paint.PathEffect);
        var blendMode = ToSKBlendMode(paint.BlendMode);
        var skPaint = new SkiaSharp.SKPaint
        {
            Style = style,
            IsAntialias = paint.IsAntialias,
            StrokeWidth = paint.StrokeWidth,
            StrokeCap = strokeCap,
            StrokeJoin = strokeJoin,
            StrokeMiter = paint.StrokeMiter,
            TextSize = paint.TextSize,
            TextAlign = textAlign,
            Typeface = typeface,
            LcdRenderText = paint.LcdRenderText,
            SubpixelText = paint.SubpixelText,
            TextEncoding = textEncoding,
            Color = color,
            Shader = shader,
            ColorFilter = colorFilter,
            ImageFilter = imageFilter,
            PathEffect = pathEffect,
            BlendMode = blendMode
        };

        ApplyTypefaceAdjustments(paint, skPaint, typefaceResolution?.SuppressSyntheticBold ?? false);

        return skPaint;
    }

    private void ApplyTypefaceAdjustments(ShimSkiaSharp.SKPaint sourcePaint, SkiaSharp.SKPaint targetPaint, bool suppressSyntheticBold)
    {
        if (ShouldEmboldenTypeface(sourcePaint.Typeface, targetPaint.Typeface, suppressSyntheticBold))
        {
            targetPaint.FakeBoldText = true;
        }
    }

    private void ApplyTypefaceAdjustments(ShimSkiaSharp.SKPaint sourcePaint, SkiaSharp.SKFont targetFont, bool suppressSyntheticBold)
    {
        if (ShouldEmboldenTypeface(sourcePaint.Typeface, targetFont.Typeface, suppressSyntheticBold))
        {
            targetFont.Embolden = true;
        }
    }

    private void ApplyTypefaceAdjustments(ShimSkiaSharp.SKFont sourceFont, SkiaSharp.SKFont targetFont, bool suppressSyntheticBold)
    {
        if (ShouldEmboldenTypeface(sourceFont.Typeface, targetFont.Typeface, suppressSyntheticBold))
        {
            targetFont.Embolden = true;
        }
    }

    private bool ShouldEmboldenTypeface(SKTypeface? sourceTypeface, SkiaSharp.SKTypeface? targetTypeface, bool suppressSyntheticBold)
    {
        if (suppressSyntheticBold || sourceTypeface is null)
        {
            return false;
        }

        var desiredWeight = (int)ToSKFontStyleWeight(sourceTypeface.FontWeight);
        if (targetTypeface is null)
        {
            return !HasExplicitTypeface(sourceTypeface) &&
                   desiredWeight > (int)SkiaSharp.SKFontStyleWeight.Normal;
        }

        return targetTypeface.FontWeight < desiredWeight;
    }

    private SkiaSharp.SKTextBlob? GetCachedPositionedTextBlob(
        DrawTextBlobCanvasCommand command,
        SkiaSharp.SKFont font)
    {
        var textBlob = command.TextBlob;
        if (textBlob?.Points is null)
        {
            return null;
        }

        var signature = new FontSignature(font);
        lock (_positionedTextCacheLock)
        {
            PositionedTextCache? cached = null;
            if (_positionedTextCache.TryGetValue(command, out var existing))
            {
                if (existing.Signature.Equals(signature))
                {
                    if (existing.TextBlob.Handle != IntPtr.Zero)
                    {
                        return existing.TextBlob;
                    }

                    cached = existing;
                }
                else
                {
                    cached = existing;
                }

                _positionedTextCache.Remove(command);
            }

            var points = ToSKPoints(textBlob.Points);
            SkiaSharp.SKTextBlob? created;
            if (textBlob.Glyphs is { Length: > 0 })
            {
                using var builder = new SkiaSharp.SKTextBlobBuilder();
                builder.AddPositionedRun(textBlob.Glyphs, font, points);
                created = builder.Build();
            }
            else if (textBlob.Text is not null)
            {
                created = SkiaSharp.SKTextBlob.CreatePositioned(textBlob.Text, font, points);
            }
            else
            {
                return null;
            }

            if (created is null)
            {
                return null;
            }

            if (cached is not null && cached.TextBlob.Handle != IntPtr.Zero)
            {
                cached.TextBlob.Dispose();
            }

            _positionedTextCache.Add(command, new PositionedTextCache(signature, created));
            _positionedTextCacheRefs.Add(new WeakReference<SkiaSharp.SKTextBlob>(created));
            TrimPositionedTextCacheRefsIfNeeded();
            return created;
        }
    }

    private bool TryGetOrCreateShapedTextBlob(
        DrawTextCanvasCommand command,
        SkiaSharp.SKFont font,
        string? fontFeatureSettings,
        string? fontKerning,
        string? fontVariantLigatures,
        out SkiaSharp.SKTextBlob textBlob,
        out float width,
        out bool disposeAfterUse)
    {
        textBlob = null!;
        width = 0f;
        disposeAfterUse = false;
        if (string.IsNullOrEmpty(command.Text) || font.Typeface is null)
        {
            return false;
        }

        var signature = new ShapedTextSignature(
            new FontSignature(font),
            fontFeatureSettings,
            fontKerning,
            fontVariantLigatures);

        if (!_cacheShapedTextBlobsForCurrentPicture)
        {
            if (!TryCreateShapedTextBlob(
                    command,
                    font,
                    fontFeatureSettings,
                    fontKerning,
                    fontVariantLigatures,
                    signature,
                    useLayoutCache: true,
                    out textBlob,
                    out width))
            {
                return false;
            }

            disposeAfterUse = true;
            return true;
        }

        lock (_positionedTextCacheLock)
        {
            ShapedTextCache? cached = null;
            var shapedTextCache = _shapedTextCache ??= new ConditionalWeakTable<DrawTextCanvasCommand, ShapedTextCache>();
            if (shapedTextCache.TryGetValue(command, out var existing))
            {
                if (existing.Signature.Equals(signature))
                {
                    if (existing.TextBlob.Handle != IntPtr.Zero)
                    {
                        textBlob = existing.TextBlob;
                        width = existing.Width;
                        return true;
                    }

                    cached = existing;
                }
                else
                {
                    cached = existing;
                }

                shapedTextCache.Remove(command);
            }

            if (!TryCreateShapedTextBlob(
                    command,
                    font,
                    fontFeatureSettings,
                    fontKerning,
                    fontVariantLigatures,
                    signature,
                    useLayoutCache: false,
                    out var created,
                    out var createdWidth))
            {
                DisposeCachedTextBlob(cached?.TextBlob);
                return false;
            }

            DisposeCachedTextBlob(cached?.TextBlob);
            shapedTextCache.Add(command, new ShapedTextCache(signature, created, createdWidth));
            _positionedTextCacheRefs.Add(new WeakReference<SkiaSharp.SKTextBlob>(created));
            TrimPositionedTextCacheRefsIfNeeded();
            textBlob = created;
            width = createdWidth;
            return true;
        }
    }

    private bool TryCreateShapedTextBlob(
        DrawTextCanvasCommand command,
        SkiaSharp.SKFont font,
        string? fontFeatureSettings,
        string? fontKerning,
        string? fontVariantLigatures,
        ShapedTextSignature signature,
        bool useLayoutCache,
        out SkiaSharp.SKTextBlob textBlob,
        out float width)
    {
        textBlob = null!;
        width = 0f;
        if (useLayoutCache &&
            TryGetCachedShapedTextLayout(command.Text, signature, out var cachedResult))
        {
            return TryCreatePositionedShapedTextBlob(cachedResult, command.X, command.Y, font, out textBlob, out width);
        }

        if (!TryShapeText(
                command.Text,
                useLayoutCache ? 0f : command.X,
                useLayoutCache ? 0f : command.Y,
                font,
                rightToLeft: null,
                fontFeatureSettings,
                fontKerning,
                fontVariantLigatures,
                out var result))
        {
            return false;
        }

        if (useLayoutCache)
        {
            CacheShapedTextLayout(command.Text, signature, result);
            return TryCreatePositionedShapedTextBlob(result, command.X, command.Y, font, out textBlob, out width);
        }

        return TryCreatePositionedShapedTextBlob(result, 0f, 0f, font, out textBlob, out width);
    }

    private static bool TryCreatePositionedShapedTextBlob(
        ShapedTextResult result,
        float x,
        float y,
        SkiaSharp.SKFont font,
        out SkiaSharp.SKTextBlob textBlob,
        out float width)
    {
        textBlob = null!;
        width = 0f;
        using var builder = new SkiaSharp.SKTextBlobBuilder();
        var points = x == 0f && y == 0f ? result.Points : OffsetShapedTextPoints(result.Points, x, y);
        builder.AddPositionedRun(result.Codepoints, font, points);
        var created = builder.Build();
        if (created is null)
        {
            return false;
        }

        textBlob = created;
        width = result.Width;
        return true;
    }

    private static SkiaSharp.SKPoint[] OffsetShapedTextPoints(SkiaSharp.SKPoint[] source, float x, float y)
    {
        var points = new SkiaSharp.SKPoint[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            points[i] = new SkiaSharp.SKPoint(source[i].X + x, source[i].Y + y);
        }

        return points;
    }

    private static void DisposeCachedTextBlob(SkiaSharp.SKTextBlob? textBlob)
    {
        if (textBlob is not null && textBlob.Handle != IntPtr.Zero)
        {
            textBlob.Dispose();
        }
    }

    public SkiaSharp.SKClipOperation ToSKClipOperation(SKClipOperation clipOperation)
    {
        return clipOperation switch
        {
            SKClipOperation.Difference => SkiaSharp.SKClipOperation.Difference,
            SKClipOperation.Intersect => SkiaSharp.SKClipOperation.Intersect,
            _ => SkiaSharp.SKClipOperation.Difference
        };
    }

    public SkiaSharp.SKPathFillType ToSKPathFillType(SKPathFillType pathFillType)
    {
        return pathFillType switch
        {
            SKPathFillType.Winding => SkiaSharp.SKPathFillType.Winding,
            SKPathFillType.EvenOdd => SkiaSharp.SKPathFillType.EvenOdd,
            _ => SkiaSharp.SKPathFillType.Winding
        };
    }

    public SKPathFillType FromSKPathFillType(SkiaSharp.SKPathFillType pathFillType)
    {
        return pathFillType switch
        {
            SkiaSharp.SKPathFillType.EvenOdd => SKPathFillType.EvenOdd,
            _ => SKPathFillType.Winding
        };
    }

    public SkiaSharp.SKPathArcSize ToSKPathArcSize(SKPathArcSize pathArcSize)
    {
        return pathArcSize switch
        {
            SKPathArcSize.Small => SkiaSharp.SKPathArcSize.Small,
            SKPathArcSize.Large => SkiaSharp.SKPathArcSize.Large,
            _ => SkiaSharp.SKPathArcSize.Small
        };
    }

    public SkiaSharp.SKPathDirection ToSKPathDirection(SKPathDirection pathDirection)
    {
        return pathDirection switch
        {
            SKPathDirection.Clockwise => SkiaSharp.SKPathDirection.Clockwise,
            SKPathDirection.CounterClockwise => SkiaSharp.SKPathDirection.CounterClockwise,
            _ => SkiaSharp.SKPathDirection.Clockwise
        };
    }

    public void ToSKPath(PathCommand pathCommand, SkiaSharp.SKPath skPath)
    {
        switch (pathCommand)
        {
            case MoveToPathCommand moveToPathCommand:
                {
                    var x = moveToPathCommand.X;
                    var y = moveToPathCommand.Y;
                    skPath.MoveTo(x, y);
                    break;
                }
            case LineToPathCommand lineToPathCommand:
                {
                    var x = lineToPathCommand.X;
                    var y = lineToPathCommand.Y;
                    skPath.LineTo(x, y);
                    break;
                }
            case ArcToPathCommand arcToPathCommand:
                {
                    var rx = arcToPathCommand.Rx;
                    var ry = arcToPathCommand.Ry;
                    var xAxisRotate = arcToPathCommand.XAxisRotate;
                    var largeArc = ToSKPathArcSize(arcToPathCommand.LargeArc);
                    var sweep = ToSKPathDirection(arcToPathCommand.Sweep);
                    var x = arcToPathCommand.X;
                    var y = arcToPathCommand.Y;
                    skPath.ArcTo(rx, ry, xAxisRotate, largeArc, sweep, x, y);
                    break;
                }
            case QuadToPathCommand quadToPathCommand:
                {
                    var x0 = quadToPathCommand.X0;
                    var y0 = quadToPathCommand.Y0;
                    var x1 = quadToPathCommand.X1;
                    var y1 = quadToPathCommand.Y1;
                    skPath.QuadTo(x0, y0, x1, y1);
                    break;
                }
            case CubicToPathCommand cubicToPathCommand:
                {
                    var x0 = cubicToPathCommand.X0;
                    var y0 = cubicToPathCommand.Y0;
                    var x1 = cubicToPathCommand.X1;
                    var y1 = cubicToPathCommand.Y1;
                    var x2 = cubicToPathCommand.X2;
                    var y2 = cubicToPathCommand.Y2;
                    skPath.CubicTo(x0, y0, x1, y1, x2, y2);
                    break;
                }
            case ClosePathCommand _:
                {
                    skPath.Close();
                    break;
                }
            case AddRectPathCommand addRectPathCommand:
                {
                    var rect = ToSKRect(addRectPathCommand.Rect);
                    skPath.AddRect(rect);
                    break;
                }
            case AddRoundRectPathCommand addRoundRectPathCommand:
                {
                    var rect = ToSKRect(addRoundRectPathCommand.Rect);
                    var rx = addRoundRectPathCommand.Rx;
                    var ry = addRoundRectPathCommand.Ry;
                    skPath.AddRoundRect(rect, rx, ry);
                    break;
                }
            case AddOvalPathCommand addOvalPathCommand:
                {
                    var rect = ToSKRect(addOvalPathCommand.Rect);
                    skPath.AddOval(rect);
                    break;
                }
            case AddCirclePathCommand addCirclePathCommand:
                {
                    var x = addCirclePathCommand.X;
                    var y = addCirclePathCommand.Y;
                    var radius = addCirclePathCommand.Radius;
                    skPath.AddCircle(x, y, radius);
                    break;
                }
            case AddPolyPathCommand addPolyPathCommand:
                {
                    if (addPolyPathCommand.Points is { })
                    {
                        var points = ToSKPoints(addPolyPathCommand.Points);
                        var close = addPolyPathCommand.Close;
                        skPath.AddPoly(points, close);
                    }
                    break;
                }
        }
    }

    public SkiaSharp.SKPath ToSKPath(SKPath path)
    {
        var skPath = new SkiaSharp.SKPath
        {
            FillType = ToSKPathFillType(path.FillType)
        };

        if (path.Commands is null)
        {
            return skPath;
        }

        foreach (var pathCommand in path.Commands)
        {
            ToSKPath(pathCommand, skPath);
        }

        return skPath;
    }

    public SKPath FromSKPath(SkiaSharp.SKPath skPath)
    {
        var path = new SKPath
        {
            FillType = FromSKPathFillType(skPath.FillType)
        };

        using var iter = skPath.CreateRawIterator();
        var pts = new SkiaSharp.SKPoint[4];
        while (true)
        {
            var verb = iter.Next(pts);
            switch (verb)
            {
                case SkiaSharp.SKPathVerb.Move:
                    path.Commands?.Add(new MoveToPathCommand(pts[0].X, pts[0].Y));
                    break;
                case SkiaSharp.SKPathVerb.Line:
                    path.Commands?.Add(new LineToPathCommand(pts[1].X, pts[1].Y));
                    break;
                case SkiaSharp.SKPathVerb.Quad:
                case SkiaSharp.SKPathVerb.Conic:
                    path.Commands?.Add(new QuadToPathCommand(pts[1].X, pts[1].Y, pts[2].X, pts[2].Y));
                    break;
                case SkiaSharp.SKPathVerb.Cubic:
                    path.Commands?.Add(new CubicToPathCommand(pts[1].X, pts[1].Y, pts[2].X, pts[2].Y, pts[3].X, pts[3].Y));
                    break;
                case SkiaSharp.SKPathVerb.Close:
                    path.Commands?.Add(new ClosePathCommand());
                    break;
                case SkiaSharp.SKPathVerb.Done:
                    return path;
            }
        }
    }

    public SkiaSharp.SKPath? ToSKPath(ClipPath? clipPath)
    {
        if (clipPath?.Clips is null)
        {
            return null;
        }

        var skPathResult = default(SkiaSharp.SKPath);

        foreach (var clip in clipPath.Clips)
        {
            if (clip.Path is null)
            {
                return null;
            }

            var skPath = ToSKPath(clip.Path);
            var skPathClip = ToSKPath(clip.Clip);
            if (skPathClip is { })
                skPath = skPath.Op(skPathClip, SkiaSharp.SKPathOp.Intersect);

            if (clip.Transform is { })
            {
                var skMatrix = ToSKMatrix(clip.Transform.Value);
                skPath.Transform(skMatrix);
            }

            if (skPathResult is null)
            {
                skPathResult = skPath;
            }
            else
            {
                var result = skPathResult.Op(skPath, SkiaSharp.SKPathOp.Union);
                skPathResult = result;
            }
        }

        if (skPathResult is null && clipPath.Clip?.Clips is { })
        {
            skPathResult = ToSKPath(clipPath.Clip);
        }
        else if (skPathResult is { })
        {
            if (clipPath.Clip?.Clips is { })
            {
                var skPathClip = ToSKPath(clipPath.Clip);
                if (skPathClip is { })
                    skPathResult = skPathResult.Op(skPathClip, SkiaSharp.SKPathOp.Intersect);
            }
        }

        if (skPathResult is { } && clipPath.Transform is { })
        {
            var skMatrix = ToSKMatrix(clipPath.Transform.Value);
            skPathResult.Transform(skMatrix);
        }

        return skPathResult;
    }

    public SkiaSharp.SKPicture? ToSKPicture(SKPicture? picture)
    {
        if (picture is null)
        {
            return null;
        }

        var skRect = ToSKRect(picture.CullRect);
        var commands = picture.Commands;
        using var skPictureRecorder = new SkiaSharp.SKPictureRecorder();
        using var skCanvas = skPictureRecorder.BeginRecording(skRect);

        var previousCacheShapedTextBlobs = _cacheShapedTextBlobsForCurrentPicture;
        var previousCacheComplexRenderPaints = _cacheComplexRenderPaintsForCurrentPicture;
        var cacheRepeatedPictureObjects = ShouldCacheRepeatedPictureObjects(picture);
        _cacheShapedTextBlobsForCurrentPicture = cacheRepeatedPictureObjects;
        _cacheComplexRenderPaintsForCurrentPicture = cacheRepeatedPictureObjects;
        try
        {
            if (commands is { Count: > 0 })
            {
                DrawPictureCommandsCore(picture, skCanvas, state: new DrawPictureState());
            }
            else
            {
                PreserveCullRect(skCanvas, skRect);
            }
        }
        finally
        {
            _cacheShapedTextBlobsForCurrentPicture = previousCacheShapedTextBlobs;
            _cacheComplexRenderPaintsForCurrentPicture = previousCacheComplexRenderPaints;
        }

        return skPictureRecorder.EndRecording();
    }

    private bool ShouldCacheRepeatedPictureObjects(SKPicture picture)
    {
        lock (_pictureCacheLock)
        {
            if (ReferenceEquals(_lastConvertedPicture, picture) ||
                ReferenceEquals(_previousConvertedPicture, picture))
            {
                return true;
            }

            _previousConvertedPicture = _lastConvertedPicture;
            _lastConvertedPicture = picture;
            return false;
        }
    }

    public SkiaSharp.SKPicture? ToWireframePicture(SKPicture? picture)
    {
        if (picture is null)
        {
            return null;
        }

        var skRect = ToSKRect(picture.CullRect);
        var commands = picture.Commands;
        using var skPictureRecorder = new SkiaSharp.SKPictureRecorder();
        using var skCanvas = skPictureRecorder.BeginRecording(skRect);

        if (commands is { Count: > 0 })
        {
            DrawPictureCommandsCore(picture, skCanvas, true);
        }
        else
        {
            PreserveCullRect(skCanvas, skRect);
        }

        return skPictureRecorder.EndRecording();
    }

    private static void PreserveCullRect(SkiaSharp.SKCanvas skCanvas, SkiaSharp.SKRect skRect)
    {
        using var paint = new SkiaSharp.SKPaint
        {
            Color = new SkiaSharp.SKColor(0, 0, 0, 0),
            IsAntialias = false,
            Style = SkiaSharp.SKPaintStyle.Fill
        };

        skCanvas.DrawRect(skRect, paint);
    }

    private void DrawPositionedTextRun(
        DrawPositionedTextRunCanvasCommand command,
        SkiaSharp.SKCanvas skCanvas,
        bool wireframe)
    {
        if (command.Fragments is not { Count: > 0 } || command.Paint is not { })
        {
            return;
        }

        using var paint = wireframe
            ? ToWireframePaint(command.Paint)
            : ToSKTextPaint(command.Paint);
        if (paint is null)
        {
            return;
        }

        var textAlign = ToSKTextAlign(command.TextAlign ?? command.Paint.TextAlign);
        var applyFont = command.Font is not null;
        using var font = command.Font is { } textFont
            ? ToSKFont(textFont)
            : ToSKFont(command.Paint);
        if (font is null)
        {
            return;
        }

        var textPaint = applyFont || command.TextAlign.HasValue
            ? CreateTextRenderPaint(paint, textAlign, font, applyFont)
            : paint;
        try
        {
            DrawPositionedTextRunFragments(command.Fragments, skCanvas, textPaint, font, textAlign);
        }
        finally
        {
            DisposeIfCloned(textPaint, paint);
        }
    }

    private void DrawPositionedTextRunFragments(
        IReadOnlyList<PositionedTextRunFragment> fragments,
        SkiaSharp.SKCanvas skCanvas,
        SkiaSharp.SKPaint paint,
        SkiaSharp.SKFont font,
        SkiaSharp.SKTextAlign textAlign)
    {
        if (TryDrawRotationScalePositionedTextRunBlob(fragments, skCanvas, paint, font, textAlign))
        {
            return;
        }

        var hasTransformedFragment = false;
        for (var i = 0; i < fragments.Count; i++)
        {
            var fragment = fragments[i];
            if (fragment.RotationDegrees != 0f || fragment.ScaleX != 1f)
            {
                hasTransformedFragment = true;
                break;
            }
        }

        if (!hasTransformedFragment)
        {
            for (var i = 0; i < fragments.Count; i++)
            {
                var fragment = fragments[i];
                skCanvas.DrawText(fragment.Text, fragment.Point.X, fragment.Point.Y, paint);
            }

            return;
        }

        var entryMatrix = skCanvas.TotalMatrix;
        try
        {
            for (var i = 0; i < fragments.Count; i++)
            {
                DrawPositionedTextRunFragment(fragments[i], skCanvas, paint, entryMatrix);
            }
        }
        finally
        {
            skCanvas.SetMatrix(entryMatrix);
        }
    }

    private void DrawPositionedTextRunFragment(
        PositionedTextRunFragment fragment,
        SkiaSharp.SKCanvas skCanvas,
        SkiaSharp.SKPaint paint,
        SkiaSharp.SKMatrix entryMatrix)
    {
        if (fragment.RotationDegrees == 0f && fragment.ScaleX == 1f)
        {
            skCanvas.SetMatrix(entryMatrix);
            skCanvas.DrawText(fragment.Text, fragment.Point.X, fragment.Point.Y, paint);
            return;
        }

        skCanvas.SetMatrix(entryMatrix);
        if (fragment.RotationDegrees != 0f)
        {
            var matrix = ToSKMatrix(SKMatrix.CreateRotationDegrees(
                fragment.RotationDegrees,
                fragment.Point.X,
                fragment.Point.Y));
            skCanvas.Concat(ref matrix);
        }

        if (fragment.ScaleX != 1f)
        {
            var matrix = ToSKMatrix(SKMatrix.CreateScale(
                fragment.ScaleX,
                1f,
                fragment.ScaleOriginX,
                fragment.Point.Y));
            skCanvas.Concat(ref matrix);
        }

        skCanvas.DrawText(fragment.Text, fragment.Point.X, fragment.Point.Y, paint);
    }

    private static bool TryDrawRotationScalePositionedTextRunBlob(
        IReadOnlyList<PositionedTextRunFragment> fragments,
        SkiaSharp.SKCanvas skCanvas,
        SkiaSharp.SKPaint paint,
        SkiaSharp.SKFont font,
        SkiaSharp.SKTextAlign textAlign)
    {
        if (textAlign != SkiaSharp.SKTextAlign.Left)
        {
            return false;
        }

        var count = fragments.Count;
        for (var i = 0; i < count; i++)
        {
            var fragment = fragments[i];
            if (fragment.Text.Length != 1 ||
                fragment.Text[0] > '\u007F' ||
                fragment.ScaleX != 1f)
            {
                return false;
            }
        }

        var text = new char[count];
        var positions = new SkiaSharp.SKRotationScaleMatrix[count];
        for (var i = 0; i < count; i++)
        {
            var fragment = fragments[i];
            text[i] = fragment.Text[0];
            positions[i] = SkiaSharp.SKRotationScaleMatrix.CreateDegrees(
                scale: 1f,
                degrees: fragment.RotationDegrees,
                tx: fragment.Point.X,
                ty: fragment.Point.Y,
                anchorX: 0f,
                anchorY: 0f);
        }

        using var textBlob = SkiaSharp.SKTextBlob.CreateRotationScale(text.AsSpan(), font, positions);
        if (textBlob is null)
        {
            return false;
        }

        skCanvas.DrawText(textBlob, 0f, 0f, paint);
        return true;
    }

    public void Draw(CanvasCommand canvasCommand, SkiaSharp.SKCanvas skCanvas, bool wireframe = false)
    {
        Draw(canvasCommand, skCanvas, wireframe, null);
    }

    private void Draw(
        CanvasCommand canvasCommand,
        SkiaSharp.SKCanvas skCanvas,
        bool wireframe,
        DrawPictureState? state)
    {
        switch (canvasCommand)
        {
            case ClipPathCanvasCommand clipPathCanvasCommand:
                {
                    if (clipPathCanvasCommand.ClipPath is { })
                    {
                        var path = ToSKPath(clipPathCanvasCommand.ClipPath);
                        var operation = ToSKClipOperation(clipPathCanvasCommand.Operation);
                        var antialias = clipPathCanvasCommand.Antialias;
                        skCanvas.ClipPath(path, operation, antialias);
                    }
                    break;
                }
            case ClipRectCanvasCommand clipRectCanvasCommand:
                {
                    var rect = ToSKRect(clipRectCanvasCommand.Rect);
                    var operation = ToSKClipOperation(clipRectCanvasCommand.Operation);
                    var antialias = clipRectCanvasCommand.Antialias;
                    skCanvas.ClipRect(rect, operation, antialias);
                    break;
                }
            case SaveCanvasCommand _:
                {
                    skCanvas.Save();
                    state?.Save(isLayer: false);
                    break;
                }
            case RestoreCanvasCommand _:
                {
                    skCanvas.Restore();
                    state?.Restore();
                    break;
                }
            case SetMatrixCanvasCommand setMatrixCanvasCommand:
                {
                    var matrix = ToSKMatrix(setMatrixCanvasCommand.DeltaMatrix);
                    skCanvas.Concat(ref matrix);
                    break;
                }
            case SaveLayerCanvasCommand saveLayerCanvasCommand:
                {
                    if (saveLayerCanvasCommand.Paint is { })
                    {
                        var paint = wireframe
                            ? ToWireframePaint(saveLayerCanvasCommand.Paint)
                            : GetRenderPaint(saveLayerCanvasCommand.Paint);
                        if (saveLayerCanvasCommand.Bounds is { } bounds)
                        {
                            skCanvas.SaveLayer(ToSKRect(bounds), paint);
                        }
                        else
                        {
                            skCanvas.SaveLayer(paint);
                        }
                    }
                    else if (saveLayerCanvasCommand.Bounds is { } bounds)
                    {
                        skCanvas.SaveLayer(ToSKRect(bounds), null);
                    }
                    else
                    {
                        skCanvas.SaveLayer();
                    }
                    state?.Save(isLayer: true);
                    break;
                }
            case DrawImageCanvasCommand drawImageCanvasCommand:
                {
                    if (drawImageCanvasCommand.Image is { })
                    {
                        if (wireframe)
                        {
                            var rectPath = new SkiaSharp.SKPath();
                            rectPath.AddRect(ToSKRect(drawImageCanvasCommand.Dest));
                            skCanvas.DrawPath(rectPath, ToWireframePaint(null));
                        }
                        else
                        {
                            var image = GetRenderImage(drawImageCanvasCommand.Image);
                            if (image is null)
                            {
                                break;
                            }

                            var source = ToSKRect(drawImageCanvasCommand.Source);
                            var dest = ToSKRect(drawImageCanvasCommand.Dest);
                            var paint = GetRenderPaint(drawImageCanvasCommand.Paint);
                            var samplingOptions = drawImageCanvasCommand.Sampling.HasValue
                                ? ToSKSamplingOptions(drawImageCanvasCommand.Sampling.Value)
                                : ToSKSamplingOptions(drawImageCanvasCommand.Paint?.FilterQuality ?? SKFilterQuality.None);
                            skCanvas.DrawImage(image, source, dest, samplingOptions, paint);
                        }
                    }
                    break;
                }
            case DrawPictureCanvasCommand drawPictureCanvasCommand:
                {
                    if (drawPictureCanvasCommand.Picture is { } picture)
                    {
                        if (!wireframe &&
                            TryGetReusableRenderPicture(picture, _cacheComplexRenderPaintsForCurrentPicture, out var cachedPicture))
                        {
                            skCanvas.DrawPicture(cachedPicture);
                        }
                        else
                        {
                            DrawPictureCommandsCore(picture, skCanvas, wireframe, state);
                        }
                    }
                    break;
                }
            case DrawPathCanvasCommand drawPathCanvasCommand:
                {
                    if (drawPathCanvasCommand.Path is { } && drawPathCanvasCommand.Paint is { })
                    {
                        var paint = wireframe
                            ? ToWireframePaint(drawPathCanvasCommand.Paint)
                            : GetRenderPaint(drawPathCanvasCommand.Paint);
                        if (paint is null)
                        {
                            break;
                        }

                        if (TryDrawDirectFilledPrimitivePath(skCanvas, paint, drawPathCanvasCommand.Paint, drawPathCanvasCommand.Path, state))
                        {
                            break;
                        }

                        var path = GetRenderPath(drawPathCanvasCommand.Path);
                        if (path is null)
                        {
                            break;
                        }

                        DrawPath(skCanvas, path, paint, drawPathCanvasCommand.Paint);
                    }
                    break;
                }
            case DrawPositionedTextRunCanvasCommand drawPositionedTextRunCanvasCommand:
                {
                    DrawPositionedTextRun(drawPositionedTextRunCanvasCommand, skCanvas, wireframe);
                    break;
                }
            case DrawTextBlobCanvasCommand drawPositionedTextCanvasCommand:
                {
                    if (drawPositionedTextCanvasCommand.TextBlob?.Points is { } && drawPositionedTextCanvasCommand.Paint is { })
                    {
                        var sourcePaint = drawPositionedTextCanvasCommand.Paint;
                        var paint = wireframe
                            ? ToWireframePaint(sourcePaint)
                            : GetRenderPaint(sourcePaint);
                        if (paint is null)
                        {
                            break;
                        }

                        using var font = drawPositionedTextCanvasCommand.TextBlob.Font is { } textBlobFont
                            ? ToSKFont(textBlobFont)
                            : ToSKFont(sourcePaint);
                        if (font is null)
                        {
                            break;
                        }

                        var textBlob = GetCachedPositionedTextBlob(drawPositionedTextCanvasCommand, font);
                        if (textBlob is not null)
                        {
                            skCanvas.DrawText(textBlob, 0, 0, paint);
                        }
                    }
                    break;
                }
            case DrawTextCanvasCommand drawTextCanvasCommand:
                {
                    if (drawTextCanvasCommand.Paint is { })
                    {
                        var text = drawTextCanvasCommand.Text;
                        var x = drawTextCanvasCommand.X;
                        var y = drawTextCanvasCommand.Y;
                        using var paint = wireframe
                            ? ToWireframePaint(drawTextCanvasCommand.Paint)
                            : ToSKTextPaint(drawTextCanvasCommand.Paint);
                        if (paint is null)
                        {
                            break;
                        }

                        var textAlign = ToSKTextAlign(drawTextCanvasCommand.TextAlign ?? drawTextCanvasCommand.Paint.TextAlign);
                        var applyFont = drawTextCanvasCommand.Font is not null;
                        using var font = drawTextCanvasCommand.Font is { } textFont
                            ? ToSKFont(textFont)
                            : ToSKFont(drawTextCanvasCommand.Paint);
                        if (font is null)
                        {
                            break;
                        }

                        var textPaint = applyFont || drawTextCanvasCommand.TextAlign.HasValue
                            ? CreateTextRenderPaint(paint, textAlign, font, applyFont)
                            : paint;
                        try
                        {
                            if (TryGetOrCreateShapedTextBlob(
                                    drawTextCanvasCommand,
                                    font,
                                    drawTextCanvasCommand.Paint.FontFeatureSettings,
                                    drawTextCanvasCommand.Paint.FontKerning,
                                    drawTextCanvasCommand.Paint.FontVariantLigatures,
                                    out var textBlob,
                                    out var shapedTextWidth,
                                    out var disposeTextBlobAfterUse))
                            {
                                try
                                {
                                    var xOffset = textAlign switch
                                    {
                                        SkiaSharp.SKTextAlign.Center => -(shapedTextWidth * 0.5f),
                                        SkiaSharp.SKTextAlign.Right => -shapedTextWidth,
                                        _ => 0f
                                    };

                                    skCanvas.DrawText(textBlob, xOffset, 0, textPaint);
                                }
                                finally
                                {
                                    if (disposeTextBlobAfterUse)
                                    {
                                        textBlob.Dispose();
                                    }
                                }
                            }
                            else
                            {
                                skCanvas.DrawText(text, x, y, textPaint);
                            }
                        }
                        finally
                        {
                            DisposeIfCloned(textPaint, paint);
                        }
                    }
                    break;
                }
            case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                {
                    if (drawTextOnPathCanvasCommand.Path is { } && drawTextOnPathCanvasCommand.Paint is { })
                    {
                        var text = drawTextOnPathCanvasCommand.Text;
                        var path = GetRenderPath(drawTextOnPathCanvasCommand.Path);
                        var hOffset = drawTextOnPathCanvasCommand.HOffset;
                        var vOffset = drawTextOnPathCanvasCommand.VOffset;
                        using var paint = wireframe
                            ? ToWireframePaint(drawTextOnPathCanvasCommand.Paint)
                            : ToSKTextPaint(drawTextOnPathCanvasCommand.Paint);
                        if (path is null || paint is null)
                        {
                            break;
                        }

                        var textAlign = ToSKTextAlign(drawTextOnPathCanvasCommand.TextAlign ?? drawTextOnPathCanvasCommand.Paint.TextAlign);
                        var applyFont = drawTextOnPathCanvasCommand.Font is not null;
                        using var font = drawTextOnPathCanvasCommand.Font is { } textFont
                            ? ToSKFont(textFont)
                            : ToSKFont(drawTextOnPathCanvasCommand.Paint);
                        if (font is null)
                        {
                            break;
                        }

                        var textPaint = applyFont || drawTextOnPathCanvasCommand.TextAlign.HasValue
                            ? CreateTextRenderPaint(paint, textAlign, font, applyFont)
                            : paint;
                        try
                        {
                            skCanvas.DrawTextOnPath(text, path, hOffset, vOffset, textPaint);
                        }
                        finally
                        {
                            DisposeIfCloned(textPaint, paint);
                        }
                    }
                    break;
                }
        }
    }

    public void Draw(SKPicture picture, SkiaSharp.SKCanvas skCanvas, bool wireframe = false)
    {
        var commands = picture.Commands;
        if (commands is null)
        {
            return;
        }

        if (wireframe)
        {
            DrawPictureCommandsCore(picture, skCanvas, wireframe: true);
            return;
        }

        var previousCacheShapedTextBlobs = _cacheShapedTextBlobsForCurrentPicture;
        var previousCacheComplexRenderPaints = _cacheComplexRenderPaintsForCurrentPicture;
        var cacheRepeatedPictureObjects = ShouldCacheRepeatedPictureObjects(picture);
        _cacheShapedTextBlobsForCurrentPicture |= cacheRepeatedPictureObjects;
        _cacheComplexRenderPaintsForCurrentPicture |= cacheRepeatedPictureObjects;
        try
        {
            DrawPictureCommandsCore(picture, skCanvas, state: new DrawPictureState());
        }
        finally
        {
            _cacheShapedTextBlobsForCurrentPicture = previousCacheShapedTextBlobs;
            _cacheComplexRenderPaintsForCurrentPicture = previousCacheComplexRenderPaints;
        }
    }

    private void DrawPictureCommandsCore(
        SKPicture picture,
        SkiaSharp.SKCanvas skCanvas,
        bool wireframe = false,
        DrawPictureState? state = null)
    {
        var commands = picture.Commands;
        if (commands is null)
        {
            return;
        }

        for (var i = 0; i < commands.Count; i++)
        {
            Draw(commands[i], skCanvas, wireframe, state);
        }
    }

    private static void DrawPath(
        SkiaSharp.SKCanvas skCanvas,
        SkiaSharp.SKPath path,
        SkiaSharp.SKPaint? paint,
        SKPaint sourcePaint)
    {
        if (paint is null)
        {
            return;
        }

        if (!sourcePaint.IsStrokeNonScaling || sourcePaint.Style != SKPaintStyle.Stroke)
        {
            skCanvas.DrawPath(path, paint);
            return;
        }

        var currentMatrix = skCanvas.TotalMatrix;
        if (currentMatrix.IsIdentity)
        {
            skCanvas.DrawPath(path, paint);
            return;
        }

        using var transformedPath = new SkiaSharp.SKPath(path);
        transformedPath.Transform(currentMatrix);

        skCanvas.Save();
        skCanvas.ResetMatrix();
        skCanvas.DrawPath(transformedPath, paint);
        skCanvas.Restore();
    }

    private bool TryDrawDirectFilledPrimitivePath(
        SkiaSharp.SKCanvas skCanvas,
        SkiaSharp.SKPaint paint,
        SKPaint sourcePaint,
        SKPath? sourcePath,
        DrawPictureState? state)
    {
        if (sourcePath?.Commands is not { Count: 1 } commands)
        {
            return false;
        }

        if (sourcePaint.Style != SKPaintStyle.Fill)
        {
            return false;
        }

        if (state is null || state.SaveLayerDepth > 0)
        {
            return false;
        }

        switch (commands[0])
        {
            case AddRectPathCommand addRect:
                skCanvas.DrawRect(ToSKRect(addRect.Rect), paint);
                return true;
            case AddRoundRectPathCommand addRoundRect:
                skCanvas.DrawRoundRect(ToSKRect(addRoundRect.Rect), addRoundRect.Rx, addRoundRect.Ry, paint);
                return true;
            case AddOvalPathCommand addOval:
                skCanvas.DrawOval(ToSKRect(addOval.Rect), paint);
                return true;
            case AddCirclePathCommand addCircle:
                skCanvas.DrawCircle(addCircle.X, addCircle.Y, addCircle.Radius, paint);
                return true;
            default:
                return false;
        }
    }

    private SkiaSharp.SKPaint ToWireframePaint(SKPaint? paint)
    {
        var strokeCap = paint is null ? SkiaSharp.SKStrokeCap.Butt : ToSKStrokeCap(paint.StrokeCap);
        var strokeJoin = paint is null ? SkiaSharp.SKStrokeJoin.Miter : ToSKStrokeJoin(paint.StrokeJoin);
        var textAlign = paint is null ? SkiaSharp.SKTextAlign.Left : ToSKTextAlign(paint.TextAlign);
        var typeface = paint is null ? null : ResolveExplicitTypeface(paint.Typeface)?.Typeface;
        var textEncoding = paint is null ? SkiaSharp.SKTextEncoding.Utf8 : ToSKTextEncoding(paint.TextEncoding);
        var colorFilter = paint is null ? null : ToSKColorFilter(paint.ColorFilter);
        var imageFilter = paint is null ? null : ToSKImageFilter(paint.ImageFilter);
        var pathEffect = paint is null ? null : ToSKPathEffect(paint.PathEffect);
        var blendMode = paint is null ? SkiaSharp.SKBlendMode.SrcOver : ToSKBlendMode(paint.BlendMode);
        return new SkiaSharp.SKPaint
        {
            Style = SkiaSharp.SKPaintStyle.Stroke,
            IsAntialias = paint?.IsAntialias ?? false,
            StrokeWidth = 0,
            StrokeCap = strokeCap,
            StrokeJoin = strokeJoin,
            StrokeMiter = paint?.StrokeMiter ?? 4,
            TextSize = paint?.TextSize ?? 0,
            TextAlign = textAlign,
            Typeface = typeface,
            LcdRenderText = paint?.LcdRenderText ?? false,
            SubpixelText = paint?.SubpixelText ?? false,
            TextEncoding = textEncoding,
            Color = new SkiaSharp.SKColor(128, 128, 128, 255),
            ColorFilter = colorFilter,
            ImageFilter = imageFilter,
            PathEffect = pathEffect,
            BlendMode = blendMode
        };
    }

    public void DrawWireframe(SKPicture picture, SkiaSharp.SKCanvas skCanvas)
    {
        Draw(picture, skCanvas, true);
    }

}
