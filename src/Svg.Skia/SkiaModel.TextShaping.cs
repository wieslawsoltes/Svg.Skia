// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using ShimSkiaSharp;
using Svg.Model;
using Buffer = HarfBuzzSharp.Buffer;

#pragma warning disable CS0618 // Keep this bridge aligned with the existing SKPaint-based ShimSkiaSharp boundary.

namespace Svg.Skia;

public partial class SkiaModel
{
    private const int HarfBuzzFontScale = 512;
    private const float MinimumStableTextMeasureSize = 16f;
    private const int TextAdvanceCacheLimit = 4096;
    private static readonly ConcurrentDictionary<TextAdvanceCacheKey, float> s_textAdvanceCache = new();

    private readonly struct TextAdvanceCacheKey : IEquatable<TextAdvanceCacheKey>
    {
        public TextAdvanceCacheKey(
            string text,
            float textSize,
            float textScaleX,
            float textSkewX,
            bool fakeBoldText,
            bool lcdRenderText,
            bool subpixelText,
            SkiaSharp.SKTextEncoding textEncoding,
            string? fontFeatureSettings,
            string? fontKerning,
            string? fontVariantLigatures,
            IntPtr typefaceHandle,
            string? typefaceFamilyName,
            int typefaceWeight,
            int typefaceWidth,
            SkiaSharp.SKFontStyleSlant typefaceSlant)
        {
            Text = text;
            TextSize = textSize;
            TextScaleX = textScaleX;
            TextSkewX = textSkewX;
            FakeBoldText = fakeBoldText;
            LcdRenderText = lcdRenderText;
            SubpixelText = subpixelText;
            TextEncoding = textEncoding;
            FontFeatureSettings = fontFeatureSettings;
            FontKerning = fontKerning;
            FontVariantLigatures = fontVariantLigatures;
            TypefaceHandle = typefaceHandle;
            TypefaceFamilyName = typefaceFamilyName;
            TypefaceWeight = typefaceWeight;
            TypefaceWidth = typefaceWidth;
            TypefaceSlant = typefaceSlant;
        }

        private string Text { get; }
        private float TextSize { get; }
        private float TextScaleX { get; }
        private float TextSkewX { get; }
        private bool FakeBoldText { get; }
        private bool LcdRenderText { get; }
        private bool SubpixelText { get; }
        private SkiaSharp.SKTextEncoding TextEncoding { get; }
        private string? FontFeatureSettings { get; }
        private string? FontKerning { get; }
        private string? FontVariantLigatures { get; }
        private IntPtr TypefaceHandle { get; }
        private string? TypefaceFamilyName { get; }
        private int TypefaceWeight { get; }
        private int TypefaceWidth { get; }
        private SkiaSharp.SKFontStyleSlant TypefaceSlant { get; }

        public bool Equals(TextAdvanceCacheKey other)
        {
            return string.Equals(Text, other.Text, StringComparison.Ordinal) &&
                   TextSize.Equals(other.TextSize) &&
                   TextScaleX.Equals(other.TextScaleX) &&
                   TextSkewX.Equals(other.TextSkewX) &&
                   FakeBoldText == other.FakeBoldText &&
                   LcdRenderText == other.LcdRenderText &&
                   SubpixelText == other.SubpixelText &&
                   TextEncoding == other.TextEncoding &&
                   string.Equals(FontFeatureSettings, other.FontFeatureSettings, StringComparison.Ordinal) &&
                   string.Equals(FontKerning, other.FontKerning, StringComparison.Ordinal) &&
                   string.Equals(FontVariantLigatures, other.FontVariantLigatures, StringComparison.Ordinal) &&
                   TypefaceHandle == other.TypefaceHandle &&
                   string.Equals(TypefaceFamilyName, other.TypefaceFamilyName, StringComparison.Ordinal) &&
                   TypefaceWeight == other.TypefaceWeight &&
                   TypefaceWidth == other.TypefaceWidth &&
                   TypefaceSlant == other.TypefaceSlant;
        }

        public override bool Equals(object? obj)
        {
            return obj is TextAdvanceCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(Text);
                hash = (hash * 397) ^ TextSize.GetHashCode();
                hash = (hash * 397) ^ TextScaleX.GetHashCode();
                hash = (hash * 397) ^ TextSkewX.GetHashCode();
                hash = (hash * 397) ^ (FakeBoldText ? 1 : 0);
                hash = (hash * 397) ^ (LcdRenderText ? 1 : 0);
                hash = (hash * 397) ^ (SubpixelText ? 1 : 0);
                hash = (hash * 397) ^ (int)TextEncoding;
                hash = (hash * 397) ^ (FontFeatureSettings is null ? 0 : StringComparer.Ordinal.GetHashCode(FontFeatureSettings));
                hash = (hash * 397) ^ (FontKerning is null ? 0 : StringComparer.Ordinal.GetHashCode(FontKerning));
                hash = (hash * 397) ^ (FontVariantLigatures is null ? 0 : StringComparer.Ordinal.GetHashCode(FontVariantLigatures));
                hash = (hash * 397) ^ TypefaceHandle.GetHashCode();
                hash = (hash * 397) ^ (TypefaceFamilyName is null ? 0 : StringComparer.Ordinal.GetHashCode(TypefaceFamilyName));
                hash = (hash * 397) ^ TypefaceWeight;
                hash = (hash * 397) ^ TypefaceWidth;
                hash = (hash * 397) ^ (int)TypefaceSlant;
                return hash;
            }
        }
    }

    internal float GetTextAdvance(string text, SkiaSharp.SKPaint paint)
    {
        return GetTextAdvance(text, paint, fontFeatureSettings: null, fontKerning: null, fontVariantLigatures: null);
    }

    internal float GetTextAdvance(
        string text,
        SkiaSharp.SKPaint paint,
        string? fontFeatureSettings,
        string? fontKerning,
        string? fontVariantLigatures)
    {
        var cacheKey = CreateTextAdvanceCacheKey(text, paint, fontFeatureSettings, fontKerning, fontVariantLigatures);
        if (s_textAdvanceCache.TryGetValue(cacheKey, out var cachedAdvance))
        {
            return cachedAdvance;
        }

        var advance = GetTextAdvanceUncached(text, paint, fontFeatureSettings, fontKerning, fontVariantLigatures);
        s_textAdvanceCache.TryAdd(cacheKey, advance);
        TrimTextAdvanceCacheIfNeeded();
        return advance;
    }

    private float GetTextAdvanceUncached(
        string text,
        SkiaSharp.SKPaint paint,
        string? fontFeatureSettings,
        string? fontKerning,
        string? fontVariantLigatures)
    {
        if (TryCreateStableMeasurePaint(paint, out var stablePaint, out var scaleDown))
        {
            using (stablePaint)
            {
                if (TryShapeText(text, 0f, 0f, stablePaint, null, fontFeatureSettings, fontKerning, fontVariantLigatures, out var stableResult))
                {
                    return stableResult.Width * scaleDown;
                }

                return stablePaint.MeasureText(text) * scaleDown;
            }
        }

        if (TryShapeText(text, 0f, 0f, paint, null, fontFeatureSettings, fontKerning, fontVariantLigatures, out var result))
        {
            return result.Width;
        }

        return paint.MeasureText(text);
    }

    private static TextAdvanceCacheKey CreateTextAdvanceCacheKey(
        string text,
        SkiaSharp.SKPaint paint,
        string? fontFeatureSettings,
        string? fontKerning,
        string? fontVariantLigatures)
    {
        var typeface = paint.Typeface;
        return new TextAdvanceCacheKey(
            text,
            paint.TextSize,
            paint.TextScaleX,
            paint.TextSkewX,
            paint.FakeBoldText,
            paint.LcdRenderText,
            paint.SubpixelText,
            paint.TextEncoding,
            fontFeatureSettings,
            fontKerning,
            fontVariantLigatures,
            typeface?.Handle ?? IntPtr.Zero,
            typeface?.FamilyName,
            typeface?.FontWeight ?? (int)SkiaSharp.SKFontStyleWeight.Normal,
            typeface?.FontWidth ?? (int)SkiaSharp.SKFontStyleWidth.Normal,
            typeface?.FontSlant ?? SkiaSharp.SKFontStyleSlant.Upright);
    }

    private static void TrimTextAdvanceCacheIfNeeded()
    {
        if (s_textAdvanceCache.Count > TextAdvanceCacheLimit)
        {
            s_textAdvanceCache.Clear();
        }
    }

    private static bool TryCreateStableMeasurePaint(
        SkiaSharp.SKPaint paint,
        out SkiaSharp.SKPaint stablePaint,
        out float scaleDown)
    {
        stablePaint = null!;
        scaleDown = 1f;
        if (paint.TextSize <= 0f || paint.TextSize >= MinimumStableTextMeasureSize)
        {
            return false;
        }

        var scaleUp = MinimumStableTextMeasureSize / paint.TextSize;
        scaleDown = 1f / scaleUp;
        stablePaint = paint.Clone();
        stablePaint.TextSize = MinimumStableTextMeasureSize;
        return true;
    }

    internal bool TryShapeGlyphRun(string? text, SKPaint paint, out ShapedGlyphRun shapedRun)
    {
        return TryShapeGlyphRun(text, paint, rightToLeft: null, out shapedRun);
    }

    internal bool TryShapeGlyphRun(string? text, SKPaint paint, bool? rightToLeft, out ShapedGlyphRun shapedRun)
    {
        shapedRun = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        using var skPaint = ToSKTextPaint(paint);
        if (skPaint is null || !TryShapeText(text!, 0f, 0f, skPaint, rightToLeft, paint.FontFeatureSettings, paint.FontKerning, paint.FontVariantLigatures, out var result))
        {
            return false;
        }

        var points = new SKPoint[result.Points.Length];
        for (var i = 0; i < result.Points.Length; i++)
        {
            points[i] = new SKPoint(result.Points[i].X, result.Points[i].Y);
        }

        shapedRun = new ShapedGlyphRun(result.Codepoints, points, result.Clusters, result.Width);
        return true;
    }

    internal bool TryShapeGlyphClusters(string? text, SKPaint paint, bool rightToLeft, out ShapedGlyphRun shapedRun, out ShapedTextCluster[] clusters)
    {
        clusters = Array.Empty<ShapedTextCluster>();
        if (!TryShapeGlyphRun(text, paint, rightToLeft, out shapedRun) ||
            string.IsNullOrEmpty(text) ||
            !TryCreateShapedTextClusters(text!, shapedRun, out clusters))
        {
            clusters = Array.Empty<ShapedTextCluster>();
            return false;
        }

        return true;
    }

    private static bool TryCreateShapedTextClusters(string text, ShapedGlyphRun shapedRun, out ShapedTextCluster[] clusters)
    {
        clusters = Array.Empty<ShapedTextCluster>();
        if (string.IsNullOrEmpty(text) ||
            shapedRun.Glyphs.Length == 0 ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length ||
            shapedRun.Clusters.Length != shapedRun.Glyphs.Length)
        {
            return false;
        }

        var groupedClusters = new List<ShapedTextCluster>();
        var emittedStarts = new HashSet<int>();
        var glyphStart = 0;
        while (glyphStart < shapedRun.Glyphs.Length)
        {
            var clusterStart = shapedRun.Clusters[glyphStart];
            if (clusterStart < 0 || clusterStart >= text.Length || !emittedStarts.Add(clusterStart))
            {
                clusters = Array.Empty<ShapedTextCluster>();
                return false;
            }

            var glyphEnd = glyphStart + 1;
            while (glyphEnd < shapedRun.Glyphs.Length && shapedRun.Clusters[glyphEnd] == clusterStart)
            {
                glyphEnd++;
            }

            var offset = shapedRun.Points[glyphStart].X;
            var nextOffset = glyphEnd < shapedRun.Points.Length ? shapedRun.Points[glyphEnd].X : shapedRun.Advance;
            var advance = Math.Max(0f, nextOffset - offset);
            groupedClusters.Add(new ShapedTextCluster(
                clusterStart,
                0,
                glyphStart,
                glyphEnd - glyphStart,
                offset,
                advance));

            glyphStart = glyphEnd;
        }

        groupedClusters.Sort(static (left, right) => left.StartCharIndex.CompareTo(right.StartCharIndex));
        var result = new ShapedTextCluster[groupedClusters.Count];
        for (var i = 0; i < groupedClusters.Count; i++)
        {
            var start = groupedClusters[i].StartCharIndex;
            var end = i + 1 < groupedClusters.Count ? groupedClusters[i + 1].StartCharIndex : text.Length;
            if (end <= start)
            {
                clusters = Array.Empty<ShapedTextCluster>();
                return false;
            }

            result[i] = groupedClusters[i] with { CharLength = end - start };
        }

        clusters = result;
        return clusters.Length > 0;
    }

    internal bool TryGetGlyphRunPath(ShapedGlyphRun shapedRun, SKPaint paint, float x, float y, out SKPath path)
    {
        path = new SKPath();
        if (shapedRun.Glyphs.Length == 0 ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length)
        {
            return false;
        }

        using var skPaint = ToSKTextPaint(paint);
        if (skPaint is null)
        {
            return false;
        }

        using var font = skPaint.ToFont();
        if (font is null || font.Typeface is null)
        {
            return false;
        }

        using var combinedPath = new SkiaSharp.SKPath();
        var appended = false;
        for (var i = 0; i < shapedRun.Glyphs.Length; i++)
        {
            using var glyphPath = font.GetGlyphPath(shapedRun.Glyphs[i]);
            if (glyphPath is null || glyphPath.IsEmpty)
            {
                continue;
            }

            var point = shapedRun.Points[i];
            combinedPath.AddPath(glyphPath, x + point.X, y + point.Y, SkiaSharp.SKPathAddMode.Append);
            appended = true;
        }

        if (!appended || combinedPath.IsEmpty)
        {
            return false;
        }

        path = FromSKPath(combinedPath);
        return !path.IsEmpty;
    }

    private bool TryShapeText(
        string text,
        float x,
        float y,
        SkiaSharp.SKPaint paint,
        bool? rightToLeft,
        out ShapedTextResult result)
    {
        return TryShapeText(
            text,
            x,
            y,
            paint,
            rightToLeft,
            fontFeatureSettings: null,
            fontKerning: null,
            fontVariantLigatures: null,
            out result);
    }

    private bool TryShapeText(
        string text,
        float x,
        float y,
        SkiaSharp.SKPaint paint,
        bool? rightToLeft,
        string? fontFeatureSettings,
        string? fontKerning,
        string? fontVariantLigatures,
        out ShapedTextResult result)
    {
        if (string.IsNullOrEmpty(text) ||
            paint.Typeface is null)
        {
            result = default;
            return false;
        }

        using var font = paint.ToFont();
        if (font is null || font.Typeface is null)
        {
            result = default;
            return false;
        }

        return TryShapeText(text, x, y, font, rightToLeft, fontFeatureSettings, fontKerning, fontVariantLigatures, out result);
    }

    private static bool TryShapeText(
        string text,
        float x,
        float y,
        SkiaSharp.SKFont font,
        bool? rightToLeft,
        string? fontFeatureSettings,
        string? fontKerning,
        string? fontVariantLigatures,
        out ShapedTextResult result)
    {
        if (string.IsNullOrEmpty(text) ||
            font.Typeface is null)
        {
            result = default;
            return false;
        }

        if (!HarfBuzzTextShaper.TryCreate(font.Typeface, out var shaper))
        {
            result = default;
            return false;
        }

        using (shaper)
        {
            result = shaper.Shape(text, x, y, font, rightToLeft, CreateHarfBuzzFeatures(fontFeatureSettings, fontKerning, fontVariantLigatures));
        }

        return result.Codepoints.Length > 0;
    }

    private static Feature[] CreateHarfBuzzFeatures(
        string? fontFeatureSettings,
        string? fontKerning,
        string? fontVariantLigatures)
    {
        if (IsDefaultFontKerning(fontKerning) &&
            IsDefaultFontVariantLigatures(fontVariantLigatures) &&
            IsDefaultFontFeatureSettings(fontFeatureSettings))
        {
            return Array.Empty<Feature>();
        }

        var features = new List<Feature>();

        AddFontKerningFeatures(features, fontKerning);
        AddFontVariantLigatureFeatures(features, fontVariantLigatures);
        AddFontFeatureSettings(features, fontFeatureSettings);

        return features.Count == 0 ? Array.Empty<Feature>() : features.ToArray();
    }

    private static bool IsDefaultFontKerning(string? fontKerning)
    {
        return string.IsNullOrWhiteSpace(fontKerning) ||
            fontKerning!.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDefaultFontVariantLigatures(string? fontVariantLigatures)
    {
        return string.IsNullOrWhiteSpace(fontVariantLigatures) ||
            fontVariantLigatures!.Trim().Equals("normal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDefaultFontFeatureSettings(string? fontFeatureSettings)
    {
        return string.IsNullOrWhiteSpace(fontFeatureSettings) ||
            fontFeatureSettings!.Trim().Equals("normal", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddFontKerningFeatures(List<Feature> features, string? fontKerning)
    {
        if (string.IsNullOrWhiteSpace(fontKerning))
        {
            return;
        }

        var normalized = fontKerning!.Trim();
        if (normalized.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AddFeature(features, "kern", normalized.Equals("none", StringComparison.OrdinalIgnoreCase) ? 0u : 1u);
    }

    private static void AddFontVariantLigatureFeatures(List<Feature> features, string? fontVariantLigatures)
    {
        if (string.IsNullOrWhiteSpace(fontVariantLigatures))
        {
            return;
        }

        var normalized = fontVariantLigatures!.Trim();
        if (normalized.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var tokens = normalized.Split(new[] { ' ', '\t', '\r', '\n', '\f' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < tokens.Length; i++)
        {
            switch (tokens[i].Trim().ToLowerInvariant())
            {
                case "none":
                    AddFeature(features, "liga", 0u);
                    AddFeature(features, "clig", 0u);
                    AddFeature(features, "dlig", 0u);
                    AddFeature(features, "hlig", 0u);
                    AddFeature(features, "calt", 0u);
                    break;
                case "common-ligatures":
                    AddFeature(features, "liga", 1u);
                    AddFeature(features, "clig", 1u);
                    break;
                case "no-common-ligatures":
                    AddFeature(features, "liga", 0u);
                    AddFeature(features, "clig", 0u);
                    break;
                case "discretionary-ligatures":
                    AddFeature(features, "dlig", 1u);
                    break;
                case "no-discretionary-ligatures":
                    AddFeature(features, "dlig", 0u);
                    break;
                case "historical-ligatures":
                    AddFeature(features, "hlig", 1u);
                    break;
                case "no-historical-ligatures":
                    AddFeature(features, "hlig", 0u);
                    break;
                case "contextual":
                    AddFeature(features, "calt", 1u);
                    break;
                case "no-contextual":
                    AddFeature(features, "calt", 0u);
                    break;
            }
        }
    }

    private static void AddFontFeatureSettings(List<Feature> features, string? fontFeatureSettings)
    {
        if (string.IsNullOrWhiteSpace(fontFeatureSettings))
        {
            return;
        }

        var normalized = fontFeatureSettings!.Trim();
        if (normalized.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parts = SplitCssCommaList(normalized);
        for (var i = 0; i < parts.Count; i++)
        {
            if (TryParseCssFontFeature(parts[i], out var tag, out var value))
            {
                AddFeature(features, tag, value);
            }
        }
    }

    private static bool TryParseCssFontFeature(string text, out string tag, out uint value)
    {
        tag = string.Empty;
        value = 1u;

        text = text.Trim();
        if (text.Length < 4)
        {
            return false;
        }

        var cursor = 0;
        if (text[0] is '\'' or '"')
        {
            var quote = text[0];
            var endQuote = text.IndexOf(quote, 1);
            if (endQuote != 5)
            {
                return false;
            }

            tag = text.Substring(1, 4);
            cursor = endQuote + 1;
        }
        else
        {
            tag = text.Substring(0, 4);
            cursor = 4;
        }

        if (!IsValidOpenTypeTag(tag))
        {
            return false;
        }

        var suffix = text.Substring(cursor).Trim();
        if (suffix.Length == 0)
        {
            return true;
        }

        if (suffix.StartsWith("=", StringComparison.Ordinal))
        {
            suffix = suffix.Substring(1).Trim();
        }

        if (suffix.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            value = 1u;
            return true;
        }

        if (suffix.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            value = 0u;
            return true;
        }

        return uint.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsValidOpenTypeTag(string tag)
    {
        if (tag.Length != 4)
        {
            return false;
        }

        for (var i = 0; i < tag.Length; i++)
        {
            if (tag[i] < 0x20 || tag[i] > 0x7E)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddFeature(List<Feature> features, string tag, uint value)
    {
        features.Add(new Feature(new Tag(tag[0], tag[1], tag[2], tag[3]), value));
    }

    private static List<string> SplitCssCommaList(string value)
    {
        var parts = new List<string>();
        var start = 0;
        var quote = '\0';
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch is '\'' or '"')
            {
                quote = ch;
                continue;
            }

            if (ch != ',')
            {
                continue;
            }

            var part = value.Substring(start, i - start).Trim();
            if (part.Length > 0)
            {
                parts.Add(part);
            }

            start = i + 1;
        }

        var lastPart = value.Substring(start).Trim();
        if (lastPart.Length > 0)
        {
            parts.Add(lastPart);
        }

        return parts;
    }

    private static Blob ToHarfBuzzBlob(SkiaSharp.SKStreamAsset asset)
    {
        if (asset is null)
        {
            throw new ArgumentNullException(nameof(asset));
        }

        var size = asset.Length;
        Blob blob;

        var memoryBase = asset.GetMemoryBase();
        if (memoryBase != IntPtr.Zero)
        {
            blob = new Blob(memoryBase, size, MemoryMode.ReadOnly, asset.Dispose);
        }
        else
        {
            var ptr = Marshal.AllocCoTaskMem(size);
            asset.Read(ptr, size);
            blob = new Blob(ptr, size, MemoryMode.ReadOnly, () =>
            {
                Marshal.FreeCoTaskMem(ptr);
                asset.Dispose();
            });
        }

        blob.MakeImmutable();
        return blob;
    }

    private readonly struct ShapedTextResult
    {
        public ShapedTextResult(
            ushort[] codepoints,
            SkiaSharp.SKPoint[] points,
            int[] clusters,
            float width)
        {
            Codepoints = codepoints;
            Points = points;
            Clusters = clusters;
            Width = width;
        }

        public ushort[] Codepoints { get; }

        public SkiaSharp.SKPoint[] Points { get; }

        public int[] Clusters { get; }

        public float Width { get; }
    }

    private sealed class HarfBuzzTextShaper : IDisposable
    {
        private readonly Font _font;

        private HarfBuzzTextShaper(SkiaSharp.SKTypeface typeface, Font font)
        {
            Typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
            _font = font ?? throw new ArgumentNullException(nameof(font));
        }

        public static bool TryCreate(SkiaSharp.SKTypeface typeface, out HarfBuzzTextShaper shaper)
        {
            if (typeface is null)
            {
                throw new ArgumentNullException(nameof(typeface));
            }

            int index;
            var stream = typeface.OpenStream(out index);
            if (stream is null)
            {
                shaper = null!;
                return false;
            }

            using var blob = ToHarfBuzzBlob(stream);
            using var face = new Face(blob, index);
            face.Index = index;
            face.UnitsPerEm = typeface.UnitsPerEm;

            var font = new Font(face);
            font.SetScale(HarfBuzzFontScale, HarfBuzzFontScale);
            font.SetFunctionsOpenType();

            shaper = new HarfBuzzTextShaper(typeface, font);
            return true;
        }

        public SkiaSharp.SKTypeface Typeface { get; }

        public void Dispose()
        {
            _font.Dispose();
        }

        public ShapedTextResult Shape(
            string text,
            float xOffset,
            float yOffset,
            SkiaSharp.SKFont font,
            bool? rightToLeft,
            Feature[] features)
        {
            if (string.IsNullOrEmpty(text))
            {
                return default;
            }

            using var buffer = new Buffer();
            buffer.ClusterLevel = ClusterLevel.Characters;
            buffer.AddUtf16(text);
            buffer.GuessSegmentProperties();
            if (rightToLeft.HasValue)
            {
                buffer.Direction = rightToLeft.Value ? Direction.RightToLeft : Direction.LeftToRight;
            }

            if (features.Length > 0)
            {
                _font.Shape(buffer, features, Array.Empty<string>());
            }
            else
            {
                _font.Shape(buffer);
            }

            var length = buffer.Length;
            var glyphInfos = buffer.GlyphInfos;
            var glyphPositions = buffer.GlyphPositions;

            var textSizeY = font.Size / HarfBuzzFontScale;
            var textSizeX = textSizeY * font.ScaleX;
            var startX = xOffset;

            var glyphs = new ushort[length];
            var points = new SkiaSharp.SKPoint[length];
            var clusters = new int[length];

            for (var i = 0; i < length; i++)
            {
                glyphs[i] = (ushort)glyphInfos[i].Codepoint;
                clusters[i] = (int)glyphInfos[i].Cluster;
                points[i] = new SkiaSharp.SKPoint(
                    xOffset + (glyphPositions[i].XOffset * textSizeX),
                    yOffset - (glyphPositions[i].YOffset * textSizeY));

                xOffset += glyphPositions[i].XAdvance * textSizeX;
                yOffset += glyphPositions[i].YAdvance * textSizeY;
            }

            return new ShapedTextResult(glyphs, points, clusters, xOffset - startX);
        }
    }
}
