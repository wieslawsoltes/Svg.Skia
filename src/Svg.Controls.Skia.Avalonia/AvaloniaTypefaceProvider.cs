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

        public SKTypeface? CreateTypeface(GlyphTypeface glyphTypeface)
        {
            lock (_sync)
            {
                if (_data is not null)
                {
                    return SKTypeface.FromData(_data);
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
                    var typeface = CreateTypeface(data, glyphTypeface, out var selectedData);
                    if (typeface is null)
                    {
                        return null;
                    }

                    // Cache the bytes, never a caller-owned typeface. SKTypeface retains the
                    // data it needs; SKData's finalizer releases the cache when the weak key dies.
                    _data = selectedData;
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

        private static SKTypeface? CreateTypeface(SKData data, GlyphTypeface glyphTypeface, out SKData selectedData)
        {
            selectedData = data;
            var count = GetCollectionFaceCount(data);
            if (count < 0)
            {
                return SKTypeface.FromData(data);
            }

            // TryGetStream does not expose the TTC face index. CoreText also rejects nonzero
            // indexes in SKTypeface.FromData. Rebuild each candidate as a standalone SFNT so
            // selection and subsequent cached requests behave consistently on every platform.
            SKTypeface? best = null;
            SKData? bestData = null;
            var bestDistance = int.MaxValue;
            try
            {
                for (var index = 0; index < count; index++)
                {
                    var candidateData = ExtractCollectionFace(data, index);
                    if (candidateData is null)
                    {
                        continue;
                    }

                    SKTypeface? candidate = null;
                    try
                    {
                        candidate = SKTypeface.FromData(candidateData);
                        if (candidate is null || !MatchesFamily(glyphTypeface, candidate.FamilyName))
                        {
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
                            bestData?.Dispose();
                            best = candidate;
                            bestData = candidateData;
                            candidate = null;
                            candidateData = null;
                            bestDistance = distance;
                            if (distance == 0)
                            {
                                break;
                            }
                        }
                    }
                    finally
                    {
                        candidate?.Dispose();
                        candidateData?.Dispose();
                    }
                }

                if (bestData is not null)
                {
                    selectedData = bestData;
                    bestData = null;
                }

                var result = best;
                best = null;
                return result;
            }
            finally
            {
                best?.Dispose();
                bestData?.Dispose();
            }
        }

        // -1 means a standalone font; 0 means a malformed collection.
        private static int GetCollectionFaceCount(SKData data)
        {
            var bytes = data.AsSpan();
            if (bytes.Length < 4 || BinaryPrimitives.ReadUInt32BigEndian(bytes) != 0x74746366) // ttcf
            {
                return -1;
            }

            if (bytes.Length < 12)
            {
                return 0;
            }

            var version = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(4, 4));
            if (version is not (0x00010000 or 0x00020000))
            {
                return 0;
            }

            var count = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(8, 4));
            var headerLength = 12UL + count * 4UL + (version == 0x00020000 ? 12UL : 0UL);
            return count > 0 && headerLength <= (ulong)bytes.Length ? (int)count : 0;
        }

        private static SKData? ExtractCollectionFace(SKData data, int index)
        {
            var bytes = data.AsSpan();
            var faceOffset = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(12 + index * 4, 4));
            if (faceOffset > (uint)(bytes.Length - 12))
            {
                return null;
            }

            var header = bytes.Slice((int)faceOffset);
            var flavor = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (flavor is not (0x00010000 or 0x4F54544F or 0x74727565 or 0x74797031)) // TrueType, OTTO, true, typ1
            {
                return null;
            }

            var tableCount = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(4, 2));
            // SFNT searchRange is a UInt16 multiple of 16, limiting the directory to 4095 tables.
            if (tableCount is 0 or > 4095 || tableCount > (header.Length - 12) / 16)
            {
                return null;
            }

            var records = header.Slice(12, tableCount * 16);
            var outputTableCount = 0;
            var headCount = 0;
            long length = 12;
            for (var table = 0; table < tableCount; table++)
            {
                var record = records.Slice(table * 16, 16);
                var tag = BinaryPrimitives.ReadUInt32BigEndian(record);
                if (tag == 0x44534947) // DSIG is invalidated by rebuilding the font.
                {
                    continue;
                }

                var offset = BinaryPrimitives.ReadUInt32BigEndian(record.Slice(8, 4));
                var tableLength = BinaryPrimitives.ReadUInt32BigEndian(record.Slice(12, 4));
                if ((ulong)offset + tableLength > (ulong)bytes.Length)
                {
                    return null;
                }

                if (tag == 0x68656164) // head
                {
                    if (tableLength < 12 || ++headCount > 1)
                    {
                        return null;
                    }
                }

                outputTableCount++;
                length += 16 + (((long)tableLength + 3) & ~3L);
                if (length > int.MaxValue)
                {
                    return null;
                }
            }

            if (headCount != 1)
            {
                return null;
            }

            var output = new byte[(int)length];
            BinaryPrimitives.WriteUInt32BigEndian(output, flavor);
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4, 2), (ushort)outputTableCount);
            var entrySelector = 0;
            var searchRange = 16;
            while (searchRange <= outputTableCount * 8)
            {
                searchRange *= 2;
                entrySelector++;
            }
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6, 2), (ushort)searchRange);
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8, 2), (ushort)entrySelector);
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10, 2), (ushort)(outputTableCount * 16 - searchRange));

            var outputRecord = 12;
            var outputOffset = 12 + outputTableCount * 16;
            var headOffset = 0;
            for (var table = 0; table < tableCount; table++)
            {
                var record = records.Slice(table * 16, 16);
                var tag = BinaryPrimitives.ReadUInt32BigEndian(record);
                if (tag == 0x44534947)
                {
                    continue;
                }

                var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(8, 4));
                var tableLength = (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(12, 4));
                var paddedLength = (tableLength + 3) & ~3;
                bytes.Slice(offset, tableLength).CopyTo(output.AsSpan(outputOffset));
                if (tag == 0x68656164)
                {
                    headOffset = outputOffset;
                    output.AsSpan(headOffset + 8, 4).Clear();
                }

                record.CopyTo(output.AsSpan(outputRecord));
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(outputRecord + 4, 4),
                    CalculateChecksum(output.AsSpan(outputOffset, paddedLength)));
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(outputRecord + 8, 4), (uint)outputOffset);
                outputRecord += 16;
                outputOffset += paddedLength;
            }

            // The head directory checksum is calculated with checkSumAdjustment zero. Only
            // the head table itself gets the adjustment that makes the whole SFNT sum correct.
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(headOffset + 8, 4),
                unchecked(0xB1B0AFBA - CalculateChecksum(output)));
            return SKData.CreateCopy(output);
        }

        private static uint CalculateChecksum(ReadOnlySpan<byte> bytes)
        {
            uint checksum = 0;
            for (var offset = 0; offset < bytes.Length; offset += 4)
            {
                checksum = unchecked(checksum + BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4)));
            }
            return checksum;
        }
    }
}
