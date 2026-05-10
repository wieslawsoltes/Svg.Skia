// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Concurrent;

namespace Svg.Skia.TypefaceProviders;

internal static class SharedTypefaceCache
{
    private const int ProviderTypefaceCacheLimit = 1024;
    private const int ResolvedTypefaceCacheLimit = 1024;
    private const int MatchCharacterCacheLimit = 4096;

    private enum ProviderKind
    {
        Default,
        FontManager
    }

    private sealed class TypefaceCacheEntry
    {
        public TypefaceCacheEntry(SkiaSharp.SKTypeface? typeface)
        {
            Typeface = typeface;
        }

        public SkiaSharp.SKTypeface? Typeface { get; }
    }

    private readonly struct ProviderTypefaceKey : IEquatable<ProviderTypefaceKey>
    {
        public ProviderTypefaceKey(
            ProviderKind providerKind,
            IntPtr fontManagerHandle,
            string familyName,
            SkiaSharp.SKFontStyleWeight weight,
            SkiaSharp.SKFontStyleWidth width,
            SkiaSharp.SKFontStyleSlant slant)
        {
            ProviderKind = providerKind;
            FontManagerHandle = fontManagerHandle;
            FamilyName = familyName;
            Weight = weight;
            Width = width;
            Slant = slant;
        }

        public ProviderKind ProviderKind { get; }
        public IntPtr FontManagerHandle { get; }
        public string FamilyName { get; }
        public SkiaSharp.SKFontStyleWeight Weight { get; }
        public SkiaSharp.SKFontStyleWidth Width { get; }
        public SkiaSharp.SKFontStyleSlant Slant { get; }

        public bool Equals(ProviderTypefaceKey other)
        {
            return ProviderKind == other.ProviderKind &&
                   FontManagerHandle == other.FontManagerHandle &&
                   string.Equals(FamilyName, other.FamilyName, StringComparison.Ordinal) &&
                   Weight == other.Weight &&
                   Width == other.Width &&
                   Slant == other.Slant;
        }

        public override bool Equals(object? obj)
        {
            return obj is ProviderTypefaceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)ProviderKind;
                hash = (hash * 397) ^ FontManagerHandle.GetHashCode();
                hash = (hash * 397) ^ FamilyName.GetHashCode();
                hash = (hash * 397) ^ (int)Weight;
                hash = (hash * 397) ^ (int)Width;
                hash = (hash * 397) ^ (int)Slant;
                return hash;
            }
        }
    }

    private readonly struct ResolvedTypefaceKey : IEquatable<ResolvedTypefaceKey>
    {
        public ResolvedTypefaceKey(
            string familyName,
            SkiaSharp.SKFontStyleWeight weight,
            SkiaSharp.SKFontStyleWidth width,
            SkiaSharp.SKFontStyleSlant slant)
        {
            FamilyName = familyName;
            Weight = weight;
            Width = width;
            Slant = slant;
        }

        public string FamilyName { get; }
        public SkiaSharp.SKFontStyleWeight Weight { get; }
        public SkiaSharp.SKFontStyleWidth Width { get; }
        public SkiaSharp.SKFontStyleSlant Slant { get; }

        public bool Equals(ResolvedTypefaceKey other)
        {
            return string.Equals(FamilyName, other.FamilyName, StringComparison.Ordinal) &&
                   Weight == other.Weight &&
                   Width == other.Width &&
                   Slant == other.Slant;
        }

        public override bool Equals(object? obj)
        {
            return obj is ResolvedTypefaceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = FamilyName.GetHashCode();
                hash = (hash * 397) ^ (int)Weight;
                hash = (hash * 397) ^ (int)Width;
                hash = (hash * 397) ^ (int)Slant;
                return hash;
            }
        }
    }

    private readonly struct MatchCharacterKey : IEquatable<MatchCharacterKey>
    {
        public MatchCharacterKey(
            string? familyName,
            SkiaSharp.SKFontStyleWeight weight,
            SkiaSharp.SKFontStyleWidth width,
            SkiaSharp.SKFontStyleSlant slant,
            int codepoint)
        {
            FamilyName = familyName;
            Weight = weight;
            Width = width;
            Slant = slant;
            Codepoint = codepoint;
        }

        public string? FamilyName { get; }
        public SkiaSharp.SKFontStyleWeight Weight { get; }
        public SkiaSharp.SKFontStyleWidth Width { get; }
        public SkiaSharp.SKFontStyleSlant Slant { get; }
        public int Codepoint { get; }

        public bool Equals(MatchCharacterKey other)
        {
            return string.Equals(FamilyName, other.FamilyName, StringComparison.Ordinal) &&
                   Weight == other.Weight &&
                   Width == other.Width &&
                   Slant == other.Slant &&
                   Codepoint == other.Codepoint;
        }

        public override bool Equals(object? obj)
        {
            return obj is MatchCharacterKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = FamilyName?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (int)Weight;
                hash = (hash * 397) ^ (int)Width;
                hash = (hash * 397) ^ (int)Slant;
                hash = (hash * 397) ^ Codepoint;
                return hash;
            }
        }
    }

    private static readonly ConcurrentDictionary<ProviderTypefaceKey, TypefaceCacheEntry> s_providerTypefaceCache = new();
    private static readonly ConcurrentDictionary<ResolvedTypefaceKey, TypefaceCacheEntry> s_resolvedTypefaceCache = new();
    private static readonly ConcurrentDictionary<MatchCharacterKey, TypefaceCacheEntry> s_matchCharacterCache = new();

    public static bool TryGetOrAddProviderTypeface(
        ITypefaceProvider provider,
        string familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        out SkiaSharp.SKTypeface? typeface)
    {
        if (!TryCreateProviderTypefaceKey(provider, familyName, weight, width, slant, out var key))
        {
            typeface = null;
            return false;
        }

        if (TryGetValidTypeface(s_providerTypefaceCache, key, out typeface))
        {
            return true;
        }

        typeface = provider.FromFamilyName(familyName, weight, width, slant);
        typeface = GetValidTypefaceOrNull(typeface);

        s_providerTypefaceCache.TryAdd(key, new TypefaceCacheEntry(typeface));
        TrimCacheIfNeeded(s_providerTypefaceCache, ProviderTypefaceCacheLimit);
        return true;
    }

    public static bool TryGetResolvedTypeface(
        string familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        out SkiaSharp.SKTypeface? typeface)
    {
        var key = new ResolvedTypefaceKey(familyName, weight, width, slant);
        return TryGetValidTypeface(s_resolvedTypefaceCache, key, out typeface);
    }

    public static void AddResolvedTypeface(
        string familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        SkiaSharp.SKTypeface? typeface)
    {
        var key = new ResolvedTypefaceKey(familyName, weight, width, slant);
        s_resolvedTypefaceCache.TryAdd(key, new TypefaceCacheEntry(GetValidTypefaceOrNull(typeface)));
        TrimCacheIfNeeded(s_resolvedTypefaceCache, ResolvedTypefaceCacheLimit);
    }

    public static bool TryGetMatchedCharacter(
        string? familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        int codepoint,
        out SkiaSharp.SKTypeface? typeface)
    {
        var key = new MatchCharacterKey(familyName, weight, width, slant, codepoint);
        return TryGetValidTypeface(s_matchCharacterCache, key, out typeface);
    }

    public static void AddMatchedCharacter(
        string? familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        int codepoint,
        SkiaSharp.SKTypeface? typeface)
    {
        var key = new MatchCharacterKey(familyName, weight, width, slant, codepoint);
        s_matchCharacterCache.TryAdd(key, new TypefaceCacheEntry(GetValidTypefaceOrNull(typeface)));
        TrimCacheIfNeeded(s_matchCharacterCache, MatchCharacterCacheLimit);
    }

    private static bool TryCreateProviderTypefaceKey(
        ITypefaceProvider provider,
        string familyName,
        SkiaSharp.SKFontStyleWeight weight,
        SkiaSharp.SKFontStyleWidth width,
        SkiaSharp.SKFontStyleSlant slant,
        out ProviderTypefaceKey key)
    {
        switch (provider)
        {
            case DefaultTypefaceProvider:
                key = new ProviderTypefaceKey(ProviderKind.Default, IntPtr.Zero, familyName, weight, width, slant);
                return true;
            case FontManagerTypefaceProvider fontManagerProvider:
                var handle = fontManagerProvider.FontManager?.Handle ?? IntPtr.Zero;
                key = new ProviderTypefaceKey(ProviderKind.FontManager, handle, familyName, weight, width, slant);
                return handle != IntPtr.Zero;
            default:
                key = default;
                return false;
        }
    }

    private static SkiaSharp.SKTypeface? GetValidTypefaceOrNull(SkiaSharp.SKTypeface? typeface)
    {
        return typeface is { } && typeface.Handle == IntPtr.Zero
            ? null
            : typeface;
    }

    private static bool TryGetValidTypeface<TKey>(
        ConcurrentDictionary<TKey, TypefaceCacheEntry> cache,
        TKey key,
        out SkiaSharp.SKTypeface? typeface)
        where TKey : notnull
    {
        if (!cache.TryGetValue(key, out var cached))
        {
            typeface = null;
            return false;
        }

        typeface = cached.Typeface;
        if (typeface is null || typeface.Handle != IntPtr.Zero)
        {
            return true;
        }

        cache.TryRemove(key, out _);
        typeface = null;
        return false;
    }

    private static void TrimCacheIfNeeded<TKey>(
        ConcurrentDictionary<TKey, TypefaceCacheEntry> cache,
        int limit)
        where TKey : notnull
    {
        if (cache.Count > limit)
        {
            cache.Clear();
        }
    }
}
