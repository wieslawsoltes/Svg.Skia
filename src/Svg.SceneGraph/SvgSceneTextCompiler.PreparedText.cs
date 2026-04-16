using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static partial class SvgSceneTextCompiler
{
    private static readonly ConcurrentDictionary<SimpleCodepointAdvanceCacheKey, float> s_simpleCodepointAdvanceCache = new();
    private static readonly ConcurrentDictionary<NaturalTextAdvanceCacheKey, float> s_naturalTextAdvanceCache = new();
    private static readonly ConcurrentDictionary<NaturalCodepointAdvanceCacheKey, float[]> s_naturalCodepointAdvanceCache = new();
    private static readonly ConcurrentDictionary<SharedLineStatsCacheKey, PreparedLineStats> s_sharedLineStatsCache = new();
    private const int SimpleCodepointAdvanceCacheLimit = 4096;
    private const int NaturalTextAdvanceCacheLimit = 2048;
    private const int NaturalCodepointAdvanceCacheLimit = 1024;
    private const int SharedLineStatsCacheLimit = 1024;
    [ThreadStatic]
    private static Dictionary<CompileLineStatsCacheKey, PreparedLineStats>? s_compileLineStatsCache;
    [ThreadStatic]
    private static Dictionary<TextMetricsPaintCacheKey, TextMetricsPaintTemplate>? s_compileTextMetricsPaintCache;
    [ThreadStatic]
    private static int s_compileLineStatsCacheScopeDepth;

    private readonly record struct SimpleCodepointAdvanceCacheKey(
        int TextMeasurementCacheKey,
        string Codepoint,
        float TextSize,
        bool LcdRenderText,
        bool SubpixelText,
        SKTextEncoding TextEncoding,
        string? TypefaceFamilyName,
        SKFontStyleWeight TypefaceWeight,
        SKFontStyleWidth TypefaceWidth,
        SKFontStyleSlant TypefaceSlant);

    private readonly record struct NaturalCodepointAdvanceCacheKey(
        int TextMeasurementCacheKey,
        string Text,
        float TextSize,
        bool LcdRenderText,
        bool SubpixelText,
        SKTextEncoding TextEncoding,
        string? TypefaceFamilyName,
        SKFontStyleWeight TypefaceWeight,
        SKFontStyleWidth TypefaceWidth,
        SKFontStyleSlant TypefaceSlant,
        bool RightToLeft,
        bool RequiresSyntheticSmallCaps,
        bool UsesBrowserCompatibleRunTypeface);

    private readonly record struct NaturalTextAdvanceCacheKey(
        int TextMeasurementCacheKey,
        string Text,
        float TextSize,
        bool LcdRenderText,
        bool SubpixelText,
        SKTextEncoding TextEncoding,
        string? TypefaceFamilyName,
        SKFontStyleWeight TypefaceWeight,
        SKFontStyleWidth TypefaceWidth,
        SKFontStyleSlant TypefaceSlant,
        string Direction,
        string UnicodeBidi,
        bool RequiresSyntheticSmallCaps,
        bool UsesBrowserCompatibleRunTypeface);

    private readonly record struct SharedLineStatsCacheKey(
        int TextMeasurementCacheKey,
        string Text,
        float TextSize,
        bool IsAntialias,
        bool LcdRenderText,
        bool SubpixelText,
        SKTextEncoding TextEncoding,
        string? TypefaceFamilyName,
        SKFontStyleWeight TypefaceWeight,
        SKFontStyleWidth TypefaceWidth,
        SKFontStyleSlant TypefaceSlant,
        string Direction,
        string UnicodeBidi);

    private readonly record struct CompileLineStatsCacheKey(
        int StyleSourceId,
        string Text,
        float BoundsLeft,
        float BoundsTop,
        float BoundsRight,
        float BoundsBottom);

    private readonly record struct TextMetricsPaintCacheKey(
        int StyleSourceId,
        float BoundsLeft,
        float BoundsTop,
        float BoundsRight,
        float BoundsBottom);

    private readonly record struct TextMetricsPaintTemplate(
        bool LcdRenderText,
        bool SubpixelText,
        SKTextEncoding TextEncoding,
        float TextSize,
        SKTypeface? Typeface);

    // Keep flat sequential text preparation behind a swappable boundary so a future
    // Pretext-backed implementation can plug in without taking ownership of SVG placement.
    private interface ISvgPreparedTextEngine
    {
        bool TryPrepareFlatRun(
            SvgTextBase styleSource,
            string text,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader,
            out PreparedFlatTextRun preparedText);

        bool TryPrepareSequentialText(
            IReadOnlyList<SequentialTextRun> runs,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader,
            bool includeLineStats,
            out PreparedSequentialText preparedText);

        float MeasureAdvance(
            SvgTextBase styleSource,
            string text,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader);

        float MeasureNaturalWidth(
            SvgTextBase styleSource,
            string text,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader);

        PreparedLineStats MeasureLineStats(
            SvgTextBase styleSource,
            string text,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader);

        float[] MeasureNaturalCodepointAdvances(
            SvgTextBase styleSource,
            IReadOnlyList<string> codepoints,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader);

        void ClearCaches();
    }

    private sealed class PreparedFlatTextRun
    {
        public PreparedFlatTextRun(
            SvgTextBase styleSource,
            string text,
            float advance,
            float naturalAdvance,
            float[] naturalCodepointAdvances)
        {
            StyleSource = styleSource;
            Text = text;
            Advance = advance;
            NaturalAdvance = naturalAdvance;
            NaturalCodepointAdvances = naturalCodepointAdvances;
        }

        public SvgTextBase StyleSource { get; }

        public string Text { get; }

        public float Advance { get; }

        public float NaturalAdvance { get; }

        public IReadOnlyList<float> NaturalCodepointAdvances { get; }
    }

    private sealed class PreparedLineStats
    {
        public PreparedLineStats(
            string drawText,
            SKTypeface? typeface,
            float advance,
            SKRect relativeBounds,
            bool usesResolvedRunTypeface,
            TypefaceSpan[]? typefaceSpans = null)
        {
            DrawText = drawText;
            Typeface = typeface;
            Advance = advance;
            RelativeBounds = relativeBounds;
            UsesResolvedRunTypeface = usesResolvedRunTypeface;
            TypefaceSpans = typefaceSpans ?? Array.Empty<TypefaceSpan>();
        }

        public string DrawText { get; }

        public SKTypeface? Typeface { get; }

        public float Advance { get; }

        public SKRect RelativeBounds { get; }

        public bool UsesResolvedRunTypeface { get; }

        public IReadOnlyList<TypefaceSpan> TypefaceSpans { get; }
    }

    private sealed class CompileLineStatsCacheScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (s_compileLineStatsCacheScopeDepth > 0)
            {
                s_compileLineStatsCacheScopeDepth--;
                if (s_compileLineStatsCacheScopeDepth == 0)
                {
                    s_compileLineStatsCache = null;
                    s_compileTextMetricsPaintCache = null;
                }
            }
        }
    }

    private sealed record PreparedSequentialRun(
        SvgTextBase StyleSource,
        string Text,
        float Advance,
        PreparedLineStats? LineStats);

    private sealed class PreparedSequentialText
    {
        public PreparedSequentialText(PreparedSequentialRun[] runs, float totalAdvance)
        {
            Runs = runs;
            TotalAdvance = totalAdvance;
        }

        public IReadOnlyList<PreparedSequentialRun> Runs { get; }

        public float TotalAdvance { get; }
    }

    private sealed class CurrentSvgPreparedTextEngine : ISvgPreparedTextEngine
    {
        public bool TryPrepareFlatRun(
            SvgTextBase styleSource,
            string text,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader,
            out PreparedFlatTextRun preparedText)
        {
            var naturalCodepointAdvances = MeasureNaturalCodepointAdvancesCore(styleSource, SplitCodepoints(text), geometryBounds, assetLoader);
            preparedText = new PreparedFlatTextRun(
                styleSource,
                text,
                MeasureTextAdvanceCore(styleSource, text, geometryBounds, assetLoader),
                MeasureNaturalTextAdvanceCore(styleSource, text, geometryBounds, assetLoader),
                naturalCodepointAdvances);
            return true;
        }

        public bool TryPrepareSequentialText(
            IReadOnlyList<SequentialTextRun> runs,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader,
            bool includeLineStats,
            out PreparedSequentialText preparedText)
        {
            if (runs.Count == 0)
            {
                preparedText = null!;
                return false;
            }

            var preparedRuns = new PreparedSequentialRun[runs.Count];
            var totalAdvance = 0f;
            for (var i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                PreparedLineStats? lineStats = null;
                var advance = 0f;

                if (includeLineStats &&
                    CanReusePreparedAlignedLeftLineStats(run.StyleSource, run.Text))
                {
                    lineStats = MeasureLineStats(run.StyleSource, run.Text, geometryBounds, assetLoader);
                    advance = lineStats.Advance;
                }
                else
                {
                    advance = MeasureTextAdvanceCore(run.StyleSource, run.Text, geometryBounds, assetLoader);
                }

                preparedRuns[i] = new PreparedSequentialRun(run.StyleSource, run.Text, advance, lineStats);
                totalAdvance += advance;
            }

            preparedText = new PreparedSequentialText(preparedRuns, totalAdvance);
            return true;
        }

        public float MeasureAdvance(
            SvgTextBase styleSource,
            string text,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader)
        {
            return MeasureTextAdvanceCore(styleSource, text, geometryBounds, assetLoader);
        }

        public float MeasureNaturalWidth(
            SvgTextBase styleSource,
            string text,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader)
        {
            return MeasureNaturalTextAdvanceCore(styleSource, text, geometryBounds, assetLoader);
        }

        public PreparedLineStats MeasureLineStats(
            SvgTextBase styleSource,
            string text,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader)
        {
            return MeasureLineStatsCore(styleSource, text, geometryBounds, assetLoader);
        }

        public float[] MeasureNaturalCodepointAdvances(
            SvgTextBase styleSource,
            IReadOnlyList<string> codepoints,
            SKRect geometryBounds,
            ISvgAssetLoader assetLoader)
        {
            return MeasureNaturalCodepointAdvancesCore(styleSource, codepoints, geometryBounds, assetLoader);
        }

        public void ClearCaches()
        {
            s_simpleCodepointAdvanceCache.Clear();
            s_naturalTextAdvanceCache.Clear();
            s_naturalCodepointAdvanceCache.Clear();
            s_sharedLineStatsCache.Clear();
        }
    }

    private static readonly ISvgPreparedTextEngine s_preparedTextEngine = new CurrentSvgPreparedTextEngine();

    private static bool TryPrepareFlatTextRun(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out PreparedFlatTextRun? preparedText)
    {
        if (!s_preparedTextEngine.TryPrepareFlatRun(svgTextBase, text, geometryBounds, assetLoader, out var prepared))
        {
            preparedText = null;
            return false;
        }

        preparedText = prepared;
        return true;
    }

    private static bool TryPrepareSequentialTextRuns(
        SvgTextBase svgTextBase,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool trimLeadingWhitespaceAtStart,
        out PreparedSequentialText? preparedText)
    {
        preparedText = null;

        if (HasPreparedSequentialTextContainerBarriers(svgTextBase) ||
            !TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart, out var runs))
        {
            return false;
        }

        return TryPrepareSequentialTextRuns(runs, geometryBounds, assetLoader, includeLineStats: false, out preparedText);
    }

    private static bool TryPrepareSequentialTextRuns(
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out PreparedSequentialText? preparedText)
    {
        return TryPrepareSequentialTextRuns(runs, geometryBounds, assetLoader, includeLineStats: false, out preparedText);
    }

    private static bool TryPrepareSequentialTextRuns(
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool includeLineStats,
        out PreparedSequentialText? preparedText)
    {
        if (!CanPrepareSequentialTextRuns(runs, geometryBounds, assetLoader) ||
            !s_preparedTextEngine.TryPrepareSequentialText(runs, geometryBounds, assetLoader, includeLineStats, out var prepared) ||
            prepared.Runs.Count != runs.Count)
        {
            preparedText = null;
            return false;
        }

        preparedText = prepared;
        return true;
    }

    private static PreparedLineStats MeasureLineStats(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (TryGetCompileCachedLineStats(svgTextBase, text, geometryBounds, out var cachedLineStats))
        {
            return cachedLineStats;
        }

        var paint = CreateTextMetricsPaint(svgTextBase, geometryBounds);
        if (TryGetSharedCachedLineStats(svgTextBase, text, assetLoader, paint, out var sharedLineStats))
        {
            CacheCompileLineStats(svgTextBase, text, geometryBounds, sharedLineStats);
            return sharedLineStats;
        }

        var lineStats = MeasureLineStatsCore(svgTextBase, text, geometryBounds, assetLoader, paint);
        CacheCompileLineStats(svgTextBase, text, geometryBounds, lineStats);
        CacheSharedLineStats(svgTextBase, text, assetLoader, paint, lineStats);
        return lineStats;
    }

    private static bool CanPrepareSequentialTextRuns(
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (runs.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            if (!CanPrepareSequentialTextRun(runs[i], geometryBounds, assetLoader))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanPrepareSequentialTextRun(
        SequentialTextRun run,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (IsVerticalWritingMode(run.StyleSource) ||
            HasOwnTextLengthAdjustment(run.StyleSource))
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(run.StyleSource, geometryBounds);
        return !SvgFontTextRenderer.TryGetLayout(run.StyleSource, run.Text, paint, assetLoader, out _);
    }

    private static bool CanReusePreparedAlignedLeftLineStats(
        SvgTextBase svgTextBase,
        string text)
    {
        return !HasPerGlyphLayoutAdjustments(svgTextBase, text) &&
               !RequiresSyntheticSmallCaps(svgTextBase, text);
    }

    private static bool HasActiveCompileLineStatsCache()
    {
        return s_compileLineStatsCacheScopeDepth > 0 &&
               s_compileLineStatsCache is not null;
    }

    private static bool HasActiveCompileTextMetricsPaintCache()
    {
        return s_compileLineStatsCacheScopeDepth > 0 &&
               s_compileTextMetricsPaintCache is not null;
    }

    private static CompileLineStatsCacheKey CreateCompileLineStatsCacheKey(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds)
    {
        return new CompileLineStatsCacheKey(
            RuntimeHelpers.GetHashCode(svgTextBase),
            text,
            geometryBounds.Left,
            geometryBounds.Top,
            geometryBounds.Right,
            geometryBounds.Bottom);
    }

    private static TextMetricsPaintCacheKey CreateTextMetricsPaintCacheKey(
        SvgTextBase svgTextBase,
        SKRect geometryBounds)
    {
        return new TextMetricsPaintCacheKey(
            RuntimeHelpers.GetHashCode(svgTextBase),
            geometryBounds.Left,
            geometryBounds.Top,
            geometryBounds.Right,
            geometryBounds.Bottom);
    }

    private static bool TryGetCompileCachedLineStats(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        out PreparedLineStats lineStats)
    {
        if (!HasActiveCompileLineStatsCache())
        {
            lineStats = null!;
            return false;
        }

        return s_compileLineStatsCache!.TryGetValue(
            CreateCompileLineStatsCacheKey(svgTextBase, text, geometryBounds),
            out lineStats!);
    }

    private static void CacheCompileLineStats(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        PreparedLineStats lineStats)
    {
        if (!HasActiveCompileLineStatsCache())
        {
            return;
        }

        s_compileLineStatsCache![CreateCompileLineStatsCacheKey(svgTextBase, text, geometryBounds)] = lineStats;
    }

    private static SharedLineStatsCacheKey CreateSharedLineStatsCacheKey(
        SvgTextBase svgTextBase,
        string text,
        ISvgAssetLoader assetLoader,
        SKPaint paint)
    {
        return new SharedLineStatsCacheKey(
            GetTextMeasurementCacheKey(assetLoader),
            text,
            paint.TextSize,
            paint.IsAntialias,
            paint.LcdRenderText,
            paint.SubpixelText,
            paint.TextEncoding,
            paint.Typeface?.FamilyName,
            paint.Typeface?.FontWeight ?? SKFontStyleWeight.Normal,
            paint.Typeface?.FontWidth ?? SKFontStyleWidth.Normal,
            paint.Typeface?.FontSlant ?? SKFontStyleSlant.Upright,
            GetInheritedTextAttribute(svgTextBase, "direction") ?? string.Empty,
            GetInheritedTextAttribute(svgTextBase, "unicode-bidi") ?? string.Empty);
    }

    private static bool TryGetSharedCachedLineStats(
        SvgTextBase svgTextBase,
        string text,
        ISvgAssetLoader assetLoader,
        SKPaint paint,
        out PreparedLineStats lineStats)
    {
        return s_sharedLineStatsCache.TryGetValue(
            CreateSharedLineStatsCacheKey(svgTextBase, text, assetLoader, paint),
            out lineStats!);
    }

    private static void CacheSharedLineStats(
        SvgTextBase svgTextBase,
        string text,
        ISvgAssetLoader assetLoader,
        SKPaint paint,
        PreparedLineStats lineStats)
    {
        s_sharedLineStatsCache.TryAdd(
            CreateSharedLineStatsCacheKey(svgTextBase, text, assetLoader, paint),
            lineStats);
        TrimSharedLineStatsCacheIfNeeded();
    }

    private static bool TryGetCompileCachedTextMetricsPaint(
        SvgTextBase svgTextBase,
        SKRect geometryBounds,
        out SKPaint paint)
    {
        if (!HasActiveCompileTextMetricsPaintCache() ||
            !s_compileTextMetricsPaintCache!.TryGetValue(
                CreateTextMetricsPaintCacheKey(svgTextBase, geometryBounds),
                out var template))
        {
            paint = null!;
            return false;
        }

        paint = new SKPaint
        {
            LcdRenderText = template.LcdRenderText,
            SubpixelText = template.SubpixelText,
            TextEncoding = template.TextEncoding,
            TextSize = template.TextSize,
            Typeface = template.Typeface,
            TextAlign = SKTextAlign.Left
        };
        return true;
    }

    private static void CacheCompileTextMetricsPaint(
        SvgTextBase svgTextBase,
        SKRect geometryBounds,
        SKPaint paint)
    {
        if (!HasActiveCompileTextMetricsPaintCache())
        {
            return;
        }

        s_compileTextMetricsPaintCache![CreateTextMetricsPaintCacheKey(svgTextBase, geometryBounds)] =
            new TextMetricsPaintTemplate(
                paint.LcdRenderText,
                paint.SubpixelText,
                paint.TextEncoding,
                paint.TextSize,
                paint.Typeface);
    }

    internal static IDisposable BeginCompileLineStatsCacheScope()
    {
        s_compileLineStatsCacheScopeDepth++;
        s_compileLineStatsCache ??= new Dictionary<CompileLineStatsCacheKey, PreparedLineStats>();
        s_compileTextMetricsPaintCache ??= new Dictionary<TextMetricsPaintCacheKey, TextMetricsPaintTemplate>();
        return new CompileLineStatsCacheScope();
    }

    private static NaturalTextAdvanceCacheKey CreateNaturalTextAdvanceCacheKey(
        SvgTextBase svgTextBase,
        ISvgAssetLoader assetLoader,
        string text,
        SKPaint paint,
        bool requiresSyntheticSmallCaps,
        bool usesBrowserCompatibleRunTypeface)
    {
        return new NaturalTextAdvanceCacheKey(
            GetTextMeasurementCacheKey(assetLoader),
            text,
            paint.TextSize,
            paint.LcdRenderText,
            paint.SubpixelText,
            paint.TextEncoding,
            paint.Typeface?.FamilyName,
            paint.Typeface?.FontWeight ?? SKFontStyleWeight.Normal,
            paint.Typeface?.FontWidth ?? SKFontStyleWidth.Normal,
            paint.Typeface?.FontSlant ?? SKFontStyleSlant.Upright,
            GetInheritedTextAttribute(svgTextBase, "direction") ?? string.Empty,
            GetInheritedTextAttribute(svgTextBase, "unicode-bidi") ?? string.Empty,
            requiresSyntheticSmallCaps,
            usesBrowserCompatibleRunTypeface);
    }

    private static bool TryGetCachedNaturalTextAdvance(
        NaturalTextAdvanceCacheKey cacheKey,
        out float advance)
    {
        return s_naturalTextAdvanceCache.TryGetValue(cacheKey, out advance);
    }

    private static void CacheNaturalTextAdvance(
        NaturalTextAdvanceCacheKey cacheKey,
        float advance)
    {
        s_naturalTextAdvanceCache.TryAdd(cacheKey, advance);
        TrimNaturalTextAdvanceCacheIfNeeded();
    }

    private static float SumAdvances(IReadOnlyList<float> advances)
    {
        var totalAdvance = 0f;
        for (var i = 0; i < advances.Count; i++)
        {
            totalAdvance += advances[i];
        }

        return totalAdvance;
    }

    private static void TrimNaturalTextAdvanceCacheIfNeeded()
    {
        if (s_naturalTextAdvanceCache.Count > NaturalTextAdvanceCacheLimit)
        {
            s_naturalTextAdvanceCache.Clear();
        }
    }

    private static void TrimSharedLineStatsCacheIfNeeded()
    {
        if (s_sharedLineStatsCache.Count > SharedLineStatsCacheLimit)
        {
            s_sharedLineStatsCache.Clear();
        }
    }

    private static void ClearPreparedTextCaches()
    {
        s_preparedTextEngine.ClearCaches();
    }

    private static float[] MeasureNaturalCodepointAdvancesCore(
        SvgTextBase svgTextBase,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var advances = new float[codepoints.Count];
        if (codepoints.Count == 0)
        {
            return advances;
        }

        if (IsVerticalWritingMode(svgTextBase))
        {
            for (var i = 0; i < codepoints.Count; i++)
            {
                advances[i] = MeasureNaturalTextAdvanceCore(svgTextBase, codepoints[i], geometryBounds, assetLoader);
            }

            return advances;
        }

        var text = string.Concat(codepoints);
        if (string.IsNullOrEmpty(text))
        {
            return advances;
        }

        var isRightToLeft = IsRightToLeft(svgTextBase);
        var requiresSyntheticSmallCaps = RequiresSyntheticSmallCaps(svgTextBase, text);
        var usesBrowserCompatibleRunTypeface = ShouldUseBrowserCompatibleRunTypeface(svgTextBase, text);
        var paint = CreateTextMetricsPaint(svgTextBase, geometryBounds);
        var textAdvanceCacheKey = CreateNaturalTextAdvanceCacheKey(
            svgTextBase,
            assetLoader,
            text,
            paint,
            requiresSyntheticSmallCaps,
            usesBrowserCompatibleRunTypeface);
        var cacheKey = CreateNaturalCodepointAdvanceCacheKey(
            assetLoader,
            text,
            paint,
            isRightToLeft,
            requiresSyntheticSmallCaps,
            usesBrowserCompatibleRunTypeface);
        if (TryGetCachedNaturalCodepointAdvances(cacheKey, out var cachedAdvances))
        {
            CacheNaturalTextAdvance(textAdvanceCacheKey, SumAdvances(cachedAdvances));
            return cachedAdvances;
        }

        if (TryMeasureNaturalCodepointAdvancesFromSimpleShapedRun(
                svgTextBase,
                text,
                codepoints,
                geometryBounds,
                paint,
                assetLoader,
                isRightToLeft,
                requiresSyntheticSmallCaps,
                usesBrowserCompatibleRunTypeface,
                out var shapedAdvances))
        {
            CacheNaturalTextAdvance(textAdvanceCacheKey, SumAdvances(shapedAdvances));
            CacheNaturalCodepointAdvances(cacheKey, shapedAdvances);
            return shapedAdvances;
        }

        var builder = new StringBuilder();
        var previousAdvance = 0f;

        for (var i = 0; i < codepoints.Count; i++)
        {
            var prefixText = builder.ToString();
            builder.Append(codepoints[i]);
            var currentAdvance = MeasureNaturalTextAdvanceCore(svgTextBase, builder.ToString(), geometryBounds, assetLoader);
            var codepointAdvance = currentAdvance - previousAdvance;
            if (IsWhitespaceCodepoint(codepoints[i]))
            {
                var contextualWhitespaceAdvance = MeasureContextualWhitespaceAdvance(svgTextBase, prefixText, codepoints[i], geometryBounds, assetLoader);
                if (IsValidPositiveAdvance(contextualWhitespaceAdvance))
                {
                    codepointAdvance = contextualWhitespaceAdvance;
                }
            }

            if (!IsValidPositiveAdvance(codepointAdvance))
            {
                codepointAdvance = 0f;
            }

            advances[i] = codepointAdvance;
            previousAdvance += codepointAdvance;
        }

        CacheNaturalTextAdvance(textAdvanceCacheKey, previousAdvance);
        CacheNaturalCodepointAdvances(cacheKey, advances);
        return advances;
    }

    private static float MeasureTextAdvanceCore(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (TryCreateVerticalTextRunPlacements(svgTextBase, text, 0f, 0f, geometryBounds, SKTextAlign.Left, assetLoader, explicitRotations: null, out _, out var verticalAdvance))
        {
            return verticalAdvance;
        }

        if (TryCreateAlignedCodepointPlacements(svgTextBase, text, 0f, 0f, geometryBounds, SKTextAlign.Left, assetLoader, explicitRotations: null, out _, out var totalAdvance))
        {
            return totalAdvance;
        }

        return MeasureNaturalTextAdvanceCore(svgTextBase, text, geometryBounds, assetLoader);
    }

    private static float MeasureNaturalTextAdvanceCore(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (IsVerticalWritingMode(svgTextBase))
        {
            var codepoints = SplitCodepoints(text);
            var totalAdvance = 0f;
            for (var i = 0; i < codepoints.Count; i++)
            {
                totalAdvance += MeasureNaturalTextAdvanceHorizontal(svgTextBase, codepoints[i], geometryBounds, assetLoader);
            }

            return totalAdvance;
        }

        return MeasureNaturalTextAdvanceHorizontal(svgTextBase, text, geometryBounds, assetLoader);
    }

    private static PreparedLineStats MeasureLineStatsCore(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var paint = CreateTextMetricsPaint(svgTextBase, geometryBounds);
        return MeasureLineStatsCore(svgTextBase, text, geometryBounds, assetLoader, paint);
    }

    private static PreparedLineStats MeasureLineStatsCore(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPaint paint)
    {
        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (string.IsNullOrEmpty(fallbackText))
        {
            return new PreparedLineStats(string.Empty, paint.Typeface, 0f, SKRect.Empty, usesResolvedRunTypeface: false);
        }

        if (TryCreateBrowserCompatibleFullRunPaint(svgTextBase, fallbackText, paint, assetLoader, out var fullRunPaint, out var shapedText))
        {
            var measureBounds = new SKRect();
            var advance = EnsureWhitespaceAdvance(
                fallbackText,
                fullRunPaint,
                assetLoader,
                assetLoader.MeasureText(shapedText, fullRunPaint, ref measureBounds));
            var relativeBounds = measureBounds;
            if (relativeBounds.IsEmpty)
            {
                relativeBounds = GetTextAdvanceBox(svgTextBase, 0f, 0f, advance, fullRunPaint, assetLoader);
            }
            else
            {
                relativeBounds = ExpandTextBoundsWithAdvanceBox(svgTextBase, relativeBounds, 0f, 0f, advance, fullRunPaint, assetLoader);
            }

            return new PreparedLineStats(shapedText, fullRunPaint.Typeface, advance, relativeBounds, usesResolvedRunTypeface: true);
        }

        var spans = assetLoader.FindTypefaces(fallbackText, paint);
        if (spans.Count > 0)
        {
            var currentX = 0f;
            var totalAdvance = 0f;
            var bounds = SKRect.Empty;
            var preparedSpans = new TypefaceSpan[spans.Count];
            for (var i = 0; i < spans.Count; i++)
            {
                var localPaint = paint.Clone();
                localPaint.Typeface = spans[i].Typeface;

                var spanBounds = SKRect.Empty;
                var spanMeasureBounds = new SKRect();
                var measuredAdvance = assetLoader.MeasureText(spans[i].Text, localPaint, ref spanMeasureBounds);
                if (!spanMeasureBounds.IsEmpty)
                {
                    spanBounds = spanMeasureBounds;
                }
                else if (TryGetRenderedTextLocalBounds(spans[i].Text, localPaint, assetLoader, out var renderedBounds))
                {
                    spanBounds = renderedBounds;
                }

                var spanAdvance = EnsureWhitespaceAdvance(spans[i].Text, localPaint, assetLoader, spans[i].Advance > 0f ? spans[i].Advance : measuredAdvance);
                if (!spanBounds.IsEmpty)
                {
                    UnionBounds(ref bounds, new SKRect(
                        currentX + spanBounds.Left,
                        spanBounds.Top,
                        currentX + spanBounds.Right,
                        spanBounds.Bottom));
                }

                UnionBounds(ref bounds, GetTextAdvanceBox(svgTextBase, currentX, 0f, spanAdvance, localPaint, assetLoader));
                preparedSpans[i] = new TypefaceSpan(spans[i].Text, spanAdvance, spans[i].Typeface);
                currentX += spanAdvance;
                totalAdvance += spanAdvance;
            }

            return new PreparedLineStats(
                ApplyBrowserCompatibleBidiControls(svgTextBase, fallbackText),
                paint.Typeface,
                totalAdvance,
                bounds,
                usesResolvedRunTypeface: false,
                preparedSpans);
        }

        var fallbackMeasureBounds = new SKRect();
        var advanceWithoutTypefaceSpans = EnsureWhitespaceAdvance(fallbackText, paint, assetLoader, assetLoader.MeasureText(fallbackText, paint, ref fallbackMeasureBounds));
        var relativeFallbackBounds = fallbackMeasureBounds;
        if (relativeFallbackBounds.IsEmpty &&
            !TryGetRenderedTextLocalBounds(fallbackText, paint, assetLoader, out relativeFallbackBounds))
        {
            relativeFallbackBounds = GetTextAdvanceBox(svgTextBase, 0f, 0f, advanceWithoutTypefaceSpans, paint, assetLoader);
        }
        else
        {
            relativeFallbackBounds = ExpandTextBoundsWithAdvanceBox(svgTextBase, relativeFallbackBounds, 0f, 0f, advanceWithoutTypefaceSpans, paint, assetLoader);
        }

        return new PreparedLineStats(
            ApplyBrowserCompatibleBidiControls(svgTextBase, fallbackText),
            paint.Typeface,
            advanceWithoutTypefaceSpans,
            relativeFallbackBounds,
            usesResolvedRunTypeface: false);
    }
}
