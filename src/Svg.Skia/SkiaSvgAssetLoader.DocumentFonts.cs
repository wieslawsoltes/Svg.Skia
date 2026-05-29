// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Svg;
using Svg.Model;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

public partial class SkiaSvgAssetLoader : ISvgDocumentFontLoader
{
    private const uint WoffSignature = 0x774F4646;

    private sealed class CssFontFace
    {
        public CssFontFace(
            string family,
            Uri sourceUri,
            string? format,
            SkiaSharp.SKFontStyleWeight weight,
            SkiaSharp.SKFontStyleWidth width,
            SkiaSharp.SKFontStyleSlant slant)
        {
            Family = family;
            SourceUri = sourceUri;
            Format = format;
            Weight = weight;
            Width = width;
            Slant = slant;
        }

        public string Family { get; }

        public Uri SourceUri { get; }

        public string? Format { get; }

        public SkiaSharp.SKFontStyleWeight Weight { get; }

        public SkiaSharp.SKFontStyleWidth Width { get; }

        public SkiaSharp.SKFontStyleSlant Slant { get; }
    }

    private sealed class WoffTable
    {
        public WoffTable(
            uint tag,
            uint checksum,
            int offset,
            int compressedLength,
            int originalLength)
        {
            Tag = tag;
            Checksum = checksum;
            Offset = offset;
            CompressedLength = compressedLength;
            OriginalLength = originalLength;
        }

        public uint Tag { get; }

        public uint Checksum { get; }

        public int Offset { get; }

        public int CompressedLength { get; }

        public int OriginalLength { get; }

        public int OutputOffset { get; set; }

        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    private static readonly Regex s_cssUrlRegex = new(@"url\((?<url>[^\)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cssFormatRegex = new(@"format\((?<format>[^\)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly object _documentFontsLock = new();
    private readonly Dictionary<string, DocumentFontTypefaceProvider> _documentFontProviders = new(StringComparer.Ordinal);

    public void ClearDocumentFonts()
    {
        lock (_documentFontsLock)
        {
            _documentFontProviders.Clear();
            _skiaModel.Settings.DocumentTypefaceProviders = null;
            ClearPaintCache();
        }
    }

    public void RegisterDocumentFonts(SvgDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var documentKey = GetDocumentFontKey(document);
        var provider = CreateDocumentFontProvider(document);
        lock (_documentFontsLock)
        {
            if (provider.IsEmpty)
            {
                _documentFontProviders.Remove(documentKey);
            }
            else
            {
                _documentFontProviders[documentKey] = provider;
            }

            _skiaModel.Settings.DocumentTypefaceProviders = _documentFontProviders.Count == 0
                ? null
                : _documentFontProviders.Values.Cast<ITypefaceProvider>().ToList();
            ClearPaintCache();
        }
    }

    private DocumentFontTypefaceProvider CreateDocumentFontProvider(SvgDocument document)
    {
        var provider = new DocumentFontTypefaceProvider();
        foreach (var styleElement in document.Descendants().OfType<SvgUnknownElement>())
        {
            if (string.IsNullOrWhiteSpace(styleElement.Content) ||
                styleElement.Content.IndexOf("@font-face", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            foreach (var fontFace in ParseCssFontFaces(styleElement.Content))
            {
                var fontUri = SvgExternalResourceResolver.ResolveResourceUri(styleElement, fontFace.SourceUri);
                if (!SvgExternalResourceResolver.AllowsExternalResource(styleElement, fontUri))
                {
                    continue;
                }

                if (!TryCreateTypeface(fontUri, document, fontFace.Format, out var typeface) ||
                    typeface is null)
                {
                    continue;
                }

                provider.Add(fontFace.Family, fontFace.Weight, fontFace.Width, fontFace.Slant, typeface);
            }
        }

        return provider;
    }

    private static IEnumerable<CssFontFace> ParseCssFontFaces(string css)
    {
        var searchIndex = 0;
        while (searchIndex < css.Length)
        {
            var atIndex = css.IndexOf("@font-face", searchIndex, StringComparison.OrdinalIgnoreCase);
            if (atIndex < 0)
            {
                yield break;
            }

            var blockStart = css.IndexOf('{', atIndex);
            if (blockStart < 0)
            {
                yield break;
            }

            var blockEnd = css.IndexOf('}', blockStart + 1);
            var bodyEnd = blockEnd < 0 ? css.Length : blockEnd;
            var body = css.Substring(blockStart + 1, bodyEnd - blockStart - 1);
            searchIndex = blockEnd < 0 ? css.Length : blockEnd + 1;

            var values = ParseCssDeclarations(body);

            if (!values.TryGetValue("font-family", out var family) ||
                string.IsNullOrWhiteSpace(family) ||
                !values.TryGetValue("src", out var src))
            {
                continue;
            }

            var urlMatch = s_cssUrlRegex.Match(src);
            if (!urlMatch.Success)
            {
                continue;
            }

            var uriValue = urlMatch.Groups["url"].Value.Trim().Trim('"', '\'');
            if (!Uri.TryCreate(uriValue, UriKind.RelativeOrAbsolute, out var sourceUri))
            {
                continue;
            }

            var format = default(string);
            var formatMatch = s_cssFormatRegex.Match(src);
            if (formatMatch.Success)
            {
                format = formatMatch.Groups["format"].Value.Trim().Trim('"', '\'').ToLowerInvariant();
            }

            yield return new CssFontFace(
                family.Trim().Trim('"', '\''),
                sourceUri,
                format,
                ParseFontWeight(values.TryGetValue("font-weight", out var weight) ? weight : null),
                ParseFontWidth(values.TryGetValue("font-stretch", out var stretch) ? stretch : null),
                ParseFontSlant(values.TryGetValue("font-style", out var style) ? style : null));
        }
    }

    private static Dictionary<string, string> ParseCssDeclarations(string body)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var propertyStart = 0;
        var valueStart = -1;
        var depth = 0;
        var quote = '\0';
        var escaped = false;

        for (var i = 0; i <= body.Length; i++)
        {
            var ch = i < body.Length ? body[i] : ';';
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch is '"' or '\'')
            {
                quote = ch;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (ch == ':' && depth == 0 && valueStart < 0)
            {
                valueStart = i + 1;
                continue;
            }

            if (ch != ';' || depth != 0)
            {
                continue;
            }

            if (valueStart > propertyStart)
            {
                var name = body.Substring(propertyStart, valueStart - propertyStart - 1).Trim();
                var value = body.Substring(valueStart, i - valueStart).Trim();
                if (name.Length > 0 && value.Length > 0)
                {
                    values[name] = value;
                }
            }

            propertyStart = i + 1;
            valueStart = -1;
        }

        return values;
    }

    private static bool TryCreateTypeface(Uri fontUri, SvgDocument document, string? format, out SkiaSharp.SKTypeface? typeface)
    {
        typeface = null;
        if (!IsSupportedFontFormat(fontUri, format))
        {
            return false;
        }

        try
        {
            using var stream = OpenFontResourceStream(fontUri, document);
            if (stream is null)
            {
                return false;
            }

            if (!TryReadTypefaceBytes(stream, fontUri, format, out var fontBytes))
            {
                return false;
            }

            using var typefaceStream = new MemoryStream(fontBytes);
            typeface = SkiaSharp.SKTypeface.FromStream(typefaceStream);
            if (typeface is null || typeface.Handle == IntPtr.Zero)
            {
                typeface?.Dispose();
                typeface = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
            typeface = null;
            return false;
        }
    }

    private static bool TryReadTypefaceBytes(Stream stream, Uri fontUri, string? format, out byte[] fontBytes)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        var sourceBytes = memoryStream.ToArray();
        if (IsWoffFont(fontUri, format, sourceBytes))
        {
            return TryConvertWoffToSfnt(sourceBytes, out fontBytes);
        }

        fontBytes = sourceBytes;
        return fontBytes.Length > 0;
    }

    private static bool IsWoffFont(Uri fontUri, string? format, byte[] bytes)
    {
        if (bytes.Length >= 4 && ReadUInt32BigEndian(bytes, 0) == WoffSignature)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(format) &&
            format.Trim().Equals("woff", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(fontUri.IsAbsoluteUri ? fontUri.LocalPath : fontUri.OriginalString);
        return extension.Equals(".woff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConvertWoffToSfnt(byte[] woffBytes, out byte[] sfntBytes)
    {
        sfntBytes = Array.Empty<byte>();
        if (woffBytes.Length < 44 ||
            ReadUInt32BigEndian(woffBytes, 0) != WoffSignature)
        {
            return false;
        }

        var flavor = ReadUInt32BigEndian(woffBytes, 4);
        var declaredLength = ReadUInt32BigEndian(woffBytes, 8);
        var numTables = ReadUInt16BigEndian(woffBytes, 12);
        if (numTables == 0 ||
            declaredLength > woffBytes.Length ||
            !CanReadRange(woffBytes.Length, 44, numTables * 20))
        {
            return false;
        }

        var tables = new List<WoffTable>(numTables);
        for (var i = 0; i < numTables; i++)
        {
            var recordOffset = 44 + i * 20;
            var tag = ReadUInt32BigEndian(woffBytes, recordOffset);
            var tableOffset = CheckedUInt32ToInt(ReadUInt32BigEndian(woffBytes, recordOffset + 4));
            var compressedLength = CheckedUInt32ToInt(ReadUInt32BigEndian(woffBytes, recordOffset + 8));
            var originalLength = CheckedUInt32ToInt(ReadUInt32BigEndian(woffBytes, recordOffset + 12));
            var checksum = ReadUInt32BigEndian(woffBytes, recordOffset + 16);
            if (tableOffset < 0 ||
                compressedLength < 0 ||
                originalLength < 0 ||
                compressedLength > originalLength ||
                !CanReadRange(woffBytes.Length, tableOffset, compressedLength))
            {
                return false;
            }

            var table = new WoffTable(tag, checksum, tableOffset, compressedLength, originalLength);
            if (!TryReadWoffTableData(woffBytes, table, out var tableData))
            {
                return false;
            }

            table.Data = tableData;
            tables.Add(table);
        }

        tables.Sort(static (left, right) => left.Tag.CompareTo(right.Tag));

        var tableDirectoryLength = 12 + tables.Count * 16;
        var tableDataOffset = Align4(tableDirectoryLength);
        var outputLength = tableDataOffset;
        for (var i = 0; i < tables.Count; i++)
        {
            tables[i].OutputOffset = outputLength;
            outputLength = Align4(outputLength + tables[i].OriginalLength);
        }

        sfntBytes = new byte[outputLength];
        WriteUInt32BigEndian(sfntBytes, 0, flavor);
        WriteUInt16BigEndian(sfntBytes, 4, (ushort)tables.Count);
        var entrySelector = GetEntrySelector(tables.Count);
        var searchRange = (ushort)((1 << entrySelector) * 16);
        var rangeShift = (ushort)(tables.Count * 16 - searchRange);
        WriteUInt16BigEndian(sfntBytes, 6, searchRange);
        WriteUInt16BigEndian(sfntBytes, 8, (ushort)entrySelector);
        WriteUInt16BigEndian(sfntBytes, 10, rangeShift);

        for (var i = 0; i < tables.Count; i++)
        {
            var table = tables[i];
            var recordOffset = 12 + i * 16;
            WriteUInt32BigEndian(sfntBytes, recordOffset, table.Tag);
            WriteUInt32BigEndian(sfntBytes, recordOffset + 4, table.Checksum);
            WriteUInt32BigEndian(sfntBytes, recordOffset + 8, (uint)table.OutputOffset);
            WriteUInt32BigEndian(sfntBytes, recordOffset + 12, (uint)table.OriginalLength);
            Array.Copy(table.Data, 0, sfntBytes, table.OutputOffset, table.OriginalLength);
        }

        return true;
    }

    private static bool TryReadWoffTableData(byte[] woffBytes, WoffTable table, out byte[] tableData)
    {
        tableData = Array.Empty<byte>();
        if (table.CompressedLength == table.OriginalLength)
        {
            tableData = new byte[table.OriginalLength];
            Array.Copy(woffBytes, table.Offset, tableData, 0, table.OriginalLength);
            return true;
        }

        if (table.CompressedLength < 6)
        {
            return false;
        }

        try
        {
            using var compressedStream = new MemoryStream(woffBytes, table.Offset + 2, table.CompressedLength - 6);
            using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream(table.OriginalLength);
            deflateStream.CopyTo(outputStream);
            tableData = outputStream.ToArray();
            return tableData.Length == table.OriginalLength;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
            tableData = Array.Empty<byte>();
            return false;
        }
    }

    private static Stream? OpenFontResourceStream(Uri fontUri, SvgDocument document)
    {
        if (fontUri.IsAbsoluteUri && string.Equals(fontUri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
        {
            return OpenDataUriStream(fontUri.OriginalString);
        }

        if (fontUri.IsFile)
        {
            var localPath = fontUri.LocalPath;
            if (File.Exists(localPath))
            {
                return File.OpenRead(localPath);
            }

            if (TryResolveW3CPackagedFontResource(localPath, document, out var fallbackPath))
            {
                return File.OpenRead(fallbackPath);
            }

            return null;
        }

#pragma warning disable 618, SYSLIB0014
        var request = WebRequest.Create(fontUri);
#pragma warning restore 618, SYSLIB0014
        using var response = request.GetResponse();
        using var responseStream = response.GetResponseStream();
        if (responseStream is null)
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        responseStream.CopyTo(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static Stream? OpenDataUriStream(string uriString)
    {
        var commaIndex = uriString.IndexOf(',');
        if (commaIndex < 0 || commaIndex + 1 >= uriString.Length)
        {
            return null;
        }

        var header = uriString.Substring(5, commaIndex - 5);
        var data = uriString.Substring(commaIndex + 1);
        var bytes = header.IndexOf(";base64", StringComparison.OrdinalIgnoreCase) >= 0
            ? Convert.FromBase64String(data)
            : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(data));
        return new MemoryStream(bytes);
    }

    private static bool TryResolveW3CPackagedFontResource(string missingLocalPath, SvgDocument document, out string fallbackPath)
    {
        fallbackPath = string.Empty;
        var fileName = Path.GetFileName(missingLocalPath);
        if (string.IsNullOrEmpty(fileName) ||
            document.BaseUri is not { IsFile: true })
        {
            return false;
        }

        var documentDirectory = Path.GetDirectoryName(document.BaseUri.LocalPath);
        var suiteRoot = Path.GetDirectoryName(documentDirectory ?? string.Empty);
        if (string.IsNullOrEmpty(suiteRoot))
        {
            return false;
        }

        var candidate = Path.Combine(suiteRoot, "resources", fileName);
        if (!File.Exists(candidate))
        {
            return false;
        }

        fallbackPath = candidate;
        return true;
    }

    private static bool CanReadRange(int length, int offset, int count)
    {
        return offset >= 0 &&
               count >= 0 &&
               offset <= length &&
               count <= length - offset;
    }

    private static int Align4(int value)
    {
        return (value + 3) & ~3;
    }

    private static int CheckedUInt32ToInt(uint value)
    {
        return value <= int.MaxValue ? (int)value : -1;
    }

    private static int GetEntrySelector(int numTables)
    {
        var entrySelector = 0;
        var power = 1;
        while (power * 2 <= numTables)
        {
            power *= 2;
            entrySelector++;
        }

        return entrySelector;
    }

    private static ushort ReadUInt16BigEndian(byte[] bytes, int offset)
    {
        return (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
    }

    private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24) |
               ((uint)bytes[offset + 1] << 16) |
               ((uint)bytes[offset + 2] << 8) |
               bytes[offset + 3];
    }

    private static void WriteUInt16BigEndian(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }

    private static void WriteUInt32BigEndian(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    private static bool IsSupportedFontFormat(Uri fontUri, string? format)
    {
        var normalizedFormat = format?.Trim();
        if (!string.IsNullOrEmpty(normalizedFormat))
        {
            var knownFormat = normalizedFormat!;
            return knownFormat.Equals("woff", StringComparison.OrdinalIgnoreCase) ||
                   knownFormat.Equals("truetype", StringComparison.OrdinalIgnoreCase) ||
                   knownFormat.Equals("opentype", StringComparison.OrdinalIgnoreCase) ||
                   knownFormat.Equals("ttf", StringComparison.OrdinalIgnoreCase) ||
                   knownFormat.Equals("otf", StringComparison.OrdinalIgnoreCase);
        }

        var extension = Path.GetExtension(fontUri.IsAbsoluteUri ? fontUri.LocalPath : fontUri.OriginalString);
        return extension.Equals(".woff", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".otf", StringComparison.OrdinalIgnoreCase);
    }

    private static SkiaSharp.SKFontStyleWeight ParseFontWeight(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return SkiaSharp.SKFontStyleWeight.Normal;
        }

        var knownWeight = trimmed!;
        if (int.TryParse(knownWeight, out var numericWeight))
        {
            return (SkiaSharp.SKFontStyleWeight)Math.Max(1, Math.Min(1000, numericWeight));
        }

        return knownWeight.Equals("bold", StringComparison.OrdinalIgnoreCase) ||
               knownWeight.Equals("bolder", StringComparison.OrdinalIgnoreCase)
            ? SkiaSharp.SKFontStyleWeight.Bold
            : SkiaSharp.SKFontStyleWeight.Normal;
    }

    private static SkiaSharp.SKFontStyleWidth ParseFontWidth(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "ultra-condensed" => SkiaSharp.SKFontStyleWidth.UltraCondensed,
            "extra-condensed" => SkiaSharp.SKFontStyleWidth.ExtraCondensed,
            "condensed" => SkiaSharp.SKFontStyleWidth.Condensed,
            "semi-condensed" => SkiaSharp.SKFontStyleWidth.SemiCondensed,
            "semi-expanded" => SkiaSharp.SKFontStyleWidth.SemiExpanded,
            "expanded" => SkiaSharp.SKFontStyleWidth.Expanded,
            "extra-expanded" => SkiaSharp.SKFontStyleWidth.ExtraExpanded,
            "ultra-expanded" => SkiaSharp.SKFontStyleWidth.UltraExpanded,
            _ => SkiaSharp.SKFontStyleWidth.Normal
        };
    }

    private static SkiaSharp.SKFontStyleSlant ParseFontSlant(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "italic" => SkiaSharp.SKFontStyleSlant.Italic,
            "oblique" => SkiaSharp.SKFontStyleSlant.Oblique,
            _ => SkiaSharp.SKFontStyleSlant.Upright
        };
    }

    private static string GetDocumentFontKey(SvgDocument document)
    {
        return document.BaseUri is { } baseUri
            ? baseUri.AbsoluteUri
            : $"document:{RuntimeHelpers.GetHashCode(document)}";
    }
}
