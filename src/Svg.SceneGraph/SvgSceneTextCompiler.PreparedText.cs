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

        if (HasSequentialTextRunBarriers(svgTextBase) ||
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
        if (!s_preparedTextEngine.TryPrepareSequentialText(runs, geometryBounds, assetLoader, out var prepared) ||
            prepared.Runs.Count != runs.Count)
        {
            preparedText = null;
            return false;
        }

        preparedText = prepared;
        return true;
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
}
