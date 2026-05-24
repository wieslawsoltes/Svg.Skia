using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static partial class SvgSceneTextCompiler
{
    private static readonly ConcurrentDictionary<SimpleCodepointAdvanceCacheKey, float> s_simpleCodepointAdvanceCache = new();
    private static readonly ConcurrentDictionary<NaturalCodepointAdvanceCacheKey, float[]> s_naturalCodepointAdvanceCache = new();
    private const int SimpleCodepointAdvanceCacheLimit = 4096;
    private const int NaturalCodepointAdvanceCacheLimit = 1024;

    private readonly record struct SimpleCodepointAdvanceCacheKey(
        int AssetLoaderId,
        string Codepoint,
        float TextSize,
        bool LcdRenderText,
        bool SubpixelText,
        SKTextEncoding TextEncoding,
        string? FontFeatureSettings,
        string? FontKerning,
        string? FontVariantLigatures,
        string? TypefaceFamilyName,
        SKFontStyleWeight TypefaceWeight,
        SKFontStyleWidth TypefaceWidth,
        SKFontStyleSlant TypefaceSlant);

    private readonly record struct NaturalCodepointAdvanceCacheKey(
        int AssetLoaderId,
        string Text,
        float TextSize,
        bool LcdRenderText,
        bool SubpixelText,
        SKTextEncoding TextEncoding,
        string? FontFeatureSettings,
        string? FontKerning,
        string? FontVariantLigatures,
        string? TypefaceFamilyName,
        SKFontStyleWeight TypefaceWeight,
        SKFontStyleWidth TypefaceWidth,
        SKFontStyleSlant TypefaceSlant,
        bool RightToLeft,
        bool RequiresSyntheticSmallCaps,
        bool UsesBrowserCompatibleRunTypeface);

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
            bool usesResolvedRunTypeface)
        {
            DrawText = drawText;
            Typeface = typeface;
            Advance = advance;
            RelativeBounds = relativeBounds;
            UsesResolvedRunTypeface = usesResolvedRunTypeface;
        }

        public string DrawText { get; }

        public SKTypeface? Typeface { get; }

        public float Advance { get; }

        public SKRect RelativeBounds { get; }

        public bool UsesResolvedRunTypeface { get; }
    }

    private sealed record PreparedSequentialRun(
        SvgTextBase StyleSource,
        string Text,
        float Advance);

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
                var advance = MeasureTextAdvanceCore(run.StyleSource, run.Text, geometryBounds, assetLoader);
                advance += GetSequentialRunBoundaryAdvance(runs, i, geometryBounds);
                preparedRuns[i] = new PreparedSequentialRun(run.StyleSource, run.Text, advance);
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
            s_naturalCodepointAdvanceCache.Clear();
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

        return TryPrepareSequentialTextRuns(runs, geometryBounds, assetLoader, out preparedText);
    }

    private static bool TryPrepareSequentialTextRuns(
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out PreparedSequentialText? preparedText)
    {
        if (!CanPrepareSequentialTextRuns(runs, geometryBounds, assetLoader) ||
            !s_preparedTextEngine.TryPrepareSequentialText(runs, geometryBounds, assetLoader, out var prepared) ||
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
        return s_preparedTextEngine.MeasureLineStats(svgTextBase, text, geometryBounds, assetLoader);
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
        ISvgAssetLoader assetLoader,
        bool ignoreOwnTextLength = false)
    {
        if (IsVerticalWritingMode(run.StyleSource) ||
            (!ignoreOwnTextLength && HasOwnTextLengthAdjustment(run.StyleSource)))
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(run.StyleSource, geometryBounds);
        return !SvgFontTextRenderer.TryGetLayout(run.StyleSource, run.Text, paint, assetLoader, out _);
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

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var isRightToLeft = IsRightToLeft(svgTextBase);
        var requiresSyntheticSmallCaps = RequiresSyntheticSmallCaps(svgTextBase, text);
        var usesBrowserCompatibleRunTypeface = ShouldUseBrowserCompatibleRunTypeface(svgTextBase, text);
        var cacheKey = CreateNaturalCodepointAdvanceCacheKey(
            assetLoader,
            text,
            paint,
            isRightToLeft,
            requiresSyntheticSmallCaps,
            usesBrowserCompatibleRunTypeface);
        if (TryGetCachedNaturalCodepointAdvances(cacheKey, out var cachedAdvances))
        {
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
            PreserveMissingWhitespaceAdvance(svgTextBase, codepoints, geometryBounds, assetLoader, shapedAdvances);
            CacheNaturalCodepointAdvances(cacheKey, shapedAdvances);
            return shapedAdvances;
        }

        if (TryMeasureNaturalCodepointAdvancesFromClusteredShapedRun(
                text,
                codepoints,
                paint,
                assetLoader,
                isRightToLeft,
                requiresSyntheticSmallCaps,
                usesBrowserCompatibleRunTypeface,
                out var clusteredAdvances))
        {
            PreserveMissingWhitespaceAdvance(svgTextBase, codepoints, geometryBounds, assetLoader, clusteredAdvances);
            CacheNaturalCodepointAdvances(cacheKey, clusteredAdvances);
            return clusteredAdvances;
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

        PreserveMissingWhitespaceAdvance(svgTextBase, codepoints, geometryBounds, assetLoader, advances);
        CacheNaturalCodepointAdvances(cacheKey, advances);
        return advances;
    }

    private static void PreserveMissingWhitespaceAdvance(
        SvgTextBase svgTextBase,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        float[] advances)
    {
        if (codepoints.Count == 0 || advances.Length != codepoints.Count)
        {
            return;
        }

        var targetIndex = -1;
        for (var i = codepoints.Count - 1; i >= 0; i--)
        {
            if (IsWhitespaceCodepoint(codepoints[i]) && !IsValidPositiveAdvance(advances[i]))
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0)
        {
            return;
        }

        var text = string.Concat(codepoints);
        var measuredAdvance = MeasureNaturalTextAdvanceCore(svgTextBase, text, geometryBounds, assetLoader);
        var distributedAdvance = advances.Sum();
        var missingAdvance = measuredAdvance - distributedAdvance;
        if (IsValidPositiveAdvance(missingAdvance))
        {
            advances[targetIndex] = missingAdvance;
        }
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

        var paint = CreateTextMetricsPaint(svgTextBase, geometryBounds);
        if (TryCreateMixedScriptSpacingRunLayout(svgTextBase, text, geometryBounds, paint, assetLoader, out var mixedLayout) &&
            mixedLayout is not null)
        {
            return mixedLayout.TotalAdvance;
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
                currentX += spanAdvance;
                totalAdvance += spanAdvance;
            }

            return new PreparedLineStats(
                ApplyBrowserCompatibleBidiControls(svgTextBase, fallbackText),
                paint.Typeface,
                totalAdvance,
                bounds,
                usesResolvedRunTypeface: false);
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
