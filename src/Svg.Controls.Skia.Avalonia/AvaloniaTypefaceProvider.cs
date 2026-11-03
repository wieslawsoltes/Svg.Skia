// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Logging;
using Avalonia.Media;
using SkiaSharp;
using Svg.Skia.TypefaceProviders;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Makes fonts registered with Avalonia available to the SVG renderer.
/// </summary>
/// <remarks>
/// Resolves embedded font families, including <c>avares:</c> and <c>fonts:</c> references,
/// through <see cref="FontManager"/>. Returned typefaces are owned by the caller; disposing
/// one does not dispose Avalonia's font or affect later requests. Font data is cached for
/// the lifetime of the resolved Avalonia glyph typeface, without retaining font managers
/// or collections. Failed resolutions are not cached, so fonts can be registered later.
/// A bare family also checks a registered <c>fonts:FamilyName</c> collection, as used by
/// <c>WithInterFont()</c>. Collections with different keys require an explicit font URI.
/// </remarks>
public sealed class AvaloniaTypefaceProvider : ITypefaceProvider
{
    private static readonly char[] s_familyTrim = { '\'', '"' };
    private readonly ConditionalWeakTable<GlyphTypeface, FontData> _fontData = new();

    /// <summary>
    /// Creates a provider that uses Avalonia's default font for generic families in the browser.
    /// </summary>
    public AvaloniaTypefaceProvider()
        : this(OperatingSystem.IsBrowser())
    {
    }

    /// <summary>
    /// Creates a provider with an explicit policy for generic font families.
    /// </summary>
    /// <param name="useDefaultFontFallback">
    /// Whether an unspecified or CSS generic family can resolve to Avalonia's default font.
    /// Unknown concrete families always return <see langword="null"/> so subsequent SVG
    /// font-family candidates and other typeface providers remain available.
    /// </param>
    public AvaloniaTypefaceProvider(bool useDefaultFontFallback)
    {
        UseDefaultFontFallback = useDefaultFontFallback;
    }

    /// <summary>
    /// Gets whether generic or unspecified families can use Avalonia's default font.
    /// </summary>
    public bool UseDefaultFontFallback { get; }

    /// <inheritdoc />
    public SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
    {
        foreach (var candidate in EnumerateFamilies(fontFamily))
        {
            try
            {
                var unspecified = string.IsNullOrWhiteSpace(candidate);
                var generic = IsGenericFamily(candidate);
                if ((unspecified || generic) && !UseDefaultFontFallback)
                {
                    continue;
                }

                // Access FontManager lazily: SVG settings can be configured before AppBuilder
                // has registered the platform font manager or the application's font collections.
                var fontManager = FontManager.Current;
                var family = unspecified || generic ? FontFamily.Default : new FontFamily(candidate);
                var style = fontStyle switch
                {
                    SKFontStyleSlant.Italic => FontStyle.Italic,
                    SKFontStyleSlant.Oblique => FontStyle.Oblique,
                    _ => FontStyle.Normal
                };
                var weight = (FontWeight)(int)fontWeight;
                var stretch = (FontStretch)(int)fontWidth;

                // WithInterFont registers fonts:Inter, but Avalonia does not resolve plain
                // "Inter" through that collection. Probe the same-name collection using its
                // public URI convention, without enumerating private FontManager state.
                if (!unspecified && !generic && family.Key is null &&
                    family.Name != FontFamily.DefaultFontFamilyName)
                {
                    try
                    {
                        var collectionUri = new Uri("fonts:" + Uri.EscapeDataString(family.Name), UriKind.Absolute);
                        var collectionFamily = new FontFamily(collectionUri.AbsoluteUri + "#" + family.Name);
                        if (fontManager.TryGetGlyphTypeface(new Typeface(collectionFamily, style, weight, stretch), out var collectionTypeface) &&
                            MatchesFamily(collectionTypeface, family.Name) &&
                            _fontData.GetValue(collectionTypeface, static _ => new FontData()).CreateTypeface(collectionTypeface) is { } collectionResult)
                        {
                            return collectionResult;
                        }
                    }
                    catch (Exception exception) when (IsRecoverableFontException(exception))
                    {
                        // A broken optional collection must not hide a usable system family.
                        Logger.TryGet(LogEventLevel.Debug, LogArea.Fonts)?.Log(this,
                            "Unable to resolve SVG font collection {FontFamily}: {Error}", candidate, exception.Message);
                    }
                }

                var typeface = new Typeface(family, style, weight, stretch);
                if (!fontManager.TryGetGlyphTypeface(typeface, out var glyphTypeface))
                {
                    continue;
                }

                // FontManager silently substitutes its default for unknown system families.
                // Accepting that substitution for "Missing, Inter" would consume the first
                // candidate and prevent Inter (or another provider) from ever being considered.
                if (!unspecified && !generic && family.Key is null &&
                    family.Name != FontFamily.DefaultFontFamilyName &&
                    !MatchesFamily(glyphTypeface, family.Name))
                {
                    continue;
                }

                var data = _fontData.GetValue(glyphTypeface, static _ => new FontData());
                if (data.CreateTypeface(glyphTypeface) is { } resolved)
                {
                    return resolved;
                }
            }
            catch (Exception exception) when (IsRecoverableFontException(exception))
            {
                // A missing platform service, unavailable font stream, or malformed resource
                // reference must leave the remaining providers usable. Do not cache the miss.
                Logger.TryGet(LogEventLevel.Debug, LogArea.Fonts)?.Log(this,
                    "Unable to resolve SVG font {FontFamily}: {Error}", candidate, exception.Message);
            }
        }

        return null;
    }

    private static bool IsRecoverableFontException(Exception exception)
    {
        return exception is InvalidOperationException or ArgumentException or IOException or NotSupportedException or FormatException;
    }

    private static IEnumerable<string> EnumerateFamilies(string? fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            yield return string.Empty;
            yield break;
        }

        foreach (var family in fontFamily!.Split(','))
        {
            var candidate = family.Trim().Trim(s_familyTrim).Trim();
            if (candidate.Length > 0)
            {
                yield return candidate;
            }
        }
    }

    private static bool IsGenericFamily(string family)
    {
        // "Default" is the unspecified-family sentinel used by SkiaSvgAssetLoader.
        return family.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("serif", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("sans-serif", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("monospace", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("cursive", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("fantasy", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("system-ui", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("ui-serif", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("ui-sans-serif", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("ui-monospace", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("ui-rounded", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("math", StringComparison.OrdinalIgnoreCase) ||
               family.Equals("fangsong", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesFamily(GlyphTypeface glyphTypeface, string family)
    {
        if (family.Equals(glyphTypeface.FamilyName, StringComparison.OrdinalIgnoreCase) ||
            family.Equals(glyphTypeface.TypographicFamilyName, StringComparison.OrdinalIgnoreCase) ||
            family.Equals(glyphTypeface.PlatformTypeface.FamilyName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var localizedFamily in glyphTypeface.FamilyNames.Values)
        {
            if (family.Equals(localizedFamily, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class FontData
    {
        private readonly object _sync = new();
        private SKData? _data;
        private int _faceIndex;

        public SKTypeface? CreateTypeface(GlyphTypeface glyphTypeface)
        {
            lock (_sync)
            {
                if (_data is not null)
                {
                    return SKTypeface.FromData(_data, _faceIndex);
                }

                if (!glyphTypeface.PlatformTypeface.TryGetStream(out var stream))
                {
                    return null;
                }

                SKData? data;
                using (stream)
                {
                    data = SKData.Create(stream);
                }

                if (data is null)
                {
                    return null;
                }

                try
                {
                    var typeface = CreateTypeface(data, glyphTypeface, out var faceIndex);
                    if (typeface is null)
                    {
                        return null;
                    }

                    // Cache the bytes, never a caller-owned typeface. SKTypeface retains the
                    // data it needs; SKData's finalizer releases the cache when the weak key dies.
                    _data = data;
                    _faceIndex = faceIndex;
                    return typeface;
                }
                finally
                {
                    if (!ReferenceEquals(_data, data))
                    {
                        data.Dispose();
                    }
                }
            }
        }

        private static SKTypeface? CreateTypeface(SKData data, GlyphTypeface glyphTypeface, out int faceIndex)
        {
            faceIndex = 0;
            var count = GetCollectionFaceCount(data);
            if (count <= 1)
            {
                return SKTypeface.FromData(data);
            }

            // TryGetStream does not expose the TTC face index. Match the selected face rather
            // than silently returning face zero when Avalonia selected another family/style.
            SKTypeface? best = null;
            var bestDistance = int.MaxValue;
            for (var index = 0; index < count; index++)
            {
                var candidate = SKTypeface.FromData(data, index);
                if (candidate is null)
                {
                    continue;
                }

                if (!MatchesFamily(glyphTypeface, candidate.FamilyName))
                {
                    candidate.Dispose();
                    continue;
                }

                var platformTypeface = glyphTypeface.PlatformTypeface;
                var slant = platformTypeface.Style switch
                {
                    FontStyle.Italic => SKFontStyleSlant.Italic,
                    FontStyle.Oblique => SKFontStyleSlant.Oblique,
                    _ => SKFontStyleSlant.Upright
                };
                var distance = Math.Abs(candidate.FontWeight - (int)platformTypeface.Weight) +
                               1000 * Math.Abs(candidate.FontWidth - (int)platformTypeface.Stretch) +
                               (candidate.FontSlant == slant ? 0 : 10000);
                if (distance < bestDistance)
                {
                    best?.Dispose();
                    best = candidate;
                    bestDistance = distance;
                    faceIndex = index;
                    if (distance == 0)
                    {
                        break;
                    }
                }
                else
                {
                    candidate.Dispose();
                }
            }

            return best;
        }

        private static int GetCollectionFaceCount(SKData data)
        {
            var bytes = data.AsSpan();
            if (bytes.Length < 12 || bytes[0] != 't' || bytes[1] != 't' || bytes[2] != 'c' || bytes[3] != 'f')
            {
                return 1;
            }

            var count = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(8, 4));
            return count > 0 && count <= (bytes.Length - 12) / 4 ? (int)count : 0;
        }
    }
}
