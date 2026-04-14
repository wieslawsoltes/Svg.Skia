using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgSceneTextCompilerTests
{
    private static readonly Type s_svgSceneTextCompilerType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgSceneTextCompiler")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgSceneTextCompiler.");

    private static readonly MethodInfo s_splitCodepointsMethod = s_svgSceneTextCompilerType.GetMethod("SplitCodepoints", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo s_measureNaturalTextAdvanceMethod = s_svgSceneTextCompilerType.GetMethod(
        "MeasureNaturalTextAdvance",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        [typeof(SvgTextBase), typeof(string), typeof(SKRect), typeof(ISvgAssetLoader)],
        modifiers: null)!;
    private static readonly MethodInfo s_measureNaturalCodepointAdvancesMethod = s_svgSceneTextCompilerType.GetMethod(
        "MeasureNaturalCodepointAdvances",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        [typeof(SvgTextBase), typeof(IReadOnlyList<string>), typeof(SKRect), typeof(ISvgAssetLoader)],
        modifiers: null)!;
    private static readonly MethodInfo s_tryCompileSequentialTextMethod = s_svgSceneTextCompilerType.GetMethod(
        "TryCompileSequentialText",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        [typeof(SvgTextBase), typeof(SKRect), typeof(DrawAttributes), typeof(ISvgAssetLoader), typeof(SKRect).MakeByRefType(), typeof(SKPicture).MakeByRefType()],
        modifiers: null)!;

    [Fact]
    public void MeasureNaturalCodepointAdvances_SimpleAsciiText_MatchesPrefixMeasurement()
    {
        VerifyMatchesPrefixMeasurement("Item 42 ");
    }

    [Fact]
    public void MeasureNaturalCodepointAdvances_CombiningMarkText_MatchesPrefixMeasurement()
    {
        VerifyMatchesPrefixMeasurement("Cafe\u0301 ");
    }

    [Fact]
    public void MeasureNaturalCodepointAdvances_KerningPairText_MatchesPrefixMeasurement()
    {
        VerifyMatchesPrefixMeasurement("AVATAR ");
    }

    [Fact]
    public void MeasureNaturalCodepointAdvances_FallsBackWhenSampledPrefixesDoNotMatch()
    {
        var document = CreateDocument("ABC", 24);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new DivergentPrefixAssetLoader(
            new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["A"] = 2f,
                ["B"] = 3f,
                ["C"] = 3f,
                ["ABC"] = 8f,
                ["AB"] = 5f
            },
            [1f, 4f, 3f]);
        var codepoints = InvokeSplitCodepoints("ABC");

        var actual = InvokeMeasureNaturalCodepointAdvances(svgText, codepoints, geometryBounds, assetLoader);

        Assert.Equal([2f, 3f, 3f], actual);
    }

    [Fact]
    public void MeasureNaturalCodepointAdvances_RecomputesForDifferentFontSizes()
    {
        var smallAdvance = MeasureTotalAdvance("Scale", 12);
        var largeAdvance = MeasureTotalAdvance("Scale", 36);

        Assert.True(largeAdvance > smallAdvance * 2f);
    }

    [Fact]
    public void MeasureNaturalCodepointAdvances_RecomputesForDifferentFontSizes_OnSharedAssetLoader()
    {
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var smallDocument = CreateDocument("Scale", 12);
        var largeDocument = CreateDocument("Scale", 36);
        var smallText = smallDocument.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var largeText = largeDocument.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var smallBounds = GetDocumentViewport(smallDocument);
        var largeBounds = GetDocumentViewport(largeDocument);
        var codepoints = InvokeSplitCodepoints("Scale");

        var smallAdvances = InvokeMeasureNaturalCodepointAdvances(smallText, codepoints, smallBounds, assetLoader);
        var largeAdvances = InvokeMeasureNaturalCodepointAdvances(largeText, codepoints, largeBounds, assetLoader);

        Assert.True(largeAdvances.Sum() > smallAdvances.Sum() * 2f);
    }

    [Fact]
    public void TryCompileSequentialText_FallsBack_ForCustomFontAndEmoji()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80">
              <text id="label" x="10" y="40" font-family="AppleGothic">📧Email</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var succeeded = InvokeTryCompileSequentialText(svgText, viewport, assetLoader);

        Assert.False(succeeded);
    }

    private static void VerifyMatchesPrefixMeasurement(string textContent)
    {
        var document = CreateDocument(textContent, 24);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var codepoints = InvokeSplitCodepoints(textContent);
        var actual = InvokeMeasureNaturalCodepointAdvances(svgText, codepoints, geometryBounds, assetLoader);
        var expected = MeasureExpectedPrefixAdvances(svgText, codepoints, geometryBounds, assetLoader);

        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], 3);
        }
    }

    private static float[] MeasureExpectedPrefixAdvances(
        SvgTextBase svgTextBase,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var advances = new float[codepoints.Count];
        var builder = new StringBuilder();
        var previousAdvance = 0f;

        for (var i = 0; i < codepoints.Count; i++)
        {
            var prefixText = builder.ToString();
            builder.Append(codepoints[i]);
            var currentAdvance = InvokeMeasureNaturalTextAdvance(svgTextBase, builder.ToString(), geometryBounds, assetLoader);
            var codepointAdvance = currentAdvance - previousAdvance;
            if (string.IsNullOrWhiteSpace(codepoints[i]))
            {
                var withWhitespaceAdvance = InvokeMeasureNaturalTextAdvance(svgTextBase, prefixText + codepoints[i] + "x", geometryBounds, assetLoader);
                var withoutWhitespaceAdvance = InvokeMeasureNaturalTextAdvance(svgTextBase, prefixText + "x", geometryBounds, assetLoader);
                var contextualWhitespaceAdvance = withWhitespaceAdvance - withoutWhitespaceAdvance;
                if (contextualWhitespaceAdvance > 0f)
                {
                    codepointAdvance = contextualWhitespaceAdvance;
                }
            }

            if (codepointAdvance < 0f)
            {
                codepointAdvance = 0f;
            }

            advances[i] = codepointAdvance;
            previousAdvance += codepointAdvance;
        }

        return advances;
    }

    private static float MeasureTotalAdvance(string textContent, int fontSize)
    {
        var document = CreateDocument(textContent, fontSize);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var codepoints = InvokeSplitCodepoints(textContent);
        var advances = InvokeMeasureNaturalCodepointAdvances(svgText, codepoints, geometryBounds, assetLoader);
        return advances.Sum();
    }

    private static SvgDocument CreateDocument(string textContent, int fontSize)
    {
        return SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            $$"""
              <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80">
                <text id="label" x="10" y="40" font-family="sans-serif" font-size="{{fontSize}}">{{textContent}}</text>
              </svg>
              """);
    }

    private static List<string> InvokeSplitCodepoints(string text)
    {
        return Assert.IsType<List<string>>(s_splitCodepointsMethod.Invoke(null, [text]));
    }

    private static float InvokeMeasureNaturalTextAdvance(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return Assert.IsType<float>(s_measureNaturalTextAdvanceMethod.Invoke(null, [svgTextBase, text, geometryBounds, assetLoader]));
    }

    private static float[] InvokeMeasureNaturalCodepointAdvances(
        SvgTextBase svgTextBase,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return Assert.IsType<float[]>(s_measureNaturalCodepointAdvancesMethod.Invoke(null, [svgTextBase, codepoints, geometryBounds, assetLoader]));
    }

    private static bool InvokeTryCompileSequentialText(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader)
    {
        var args = new object?[]
        {
            svgTextBase,
            viewport,
            DrawAttributes.None,
            assetLoader,
            default(SKRect),
            null
        };

        return Assert.IsType<bool>(s_tryCompileSequentialTextMethod.Invoke(null, args));
    }

    private static SKRect GetDocumentViewport(SvgDocument document)
    {
        var size = SvgService.GetDimensions(document);
        var bounds = SKRect.Create(size);
        if (!bounds.IsEmpty)
        {
            return bounds;
        }

        if (document.ViewBox.Width > 0f && document.ViewBox.Height > 0f)
        {
            return SKRect.Create(
                document.ViewBox.MinX,
                document.ViewBox.MinY,
                document.ViewBox.Width,
                document.ViewBox.Height);
        }

        return SKRect.Empty;
    }

    private sealed class DivergentPrefixAssetLoader : ISvgAssetLoader, ISvgTextRunTypefaceResolver, ISvgTextGlyphRunResolver
    {
        private readonly IReadOnlyDictionary<string, float> _measuredAdvances;
        private readonly float[] _runAdvances;
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public DivergentPrefixAssetLoader(IReadOnlyDictionary<string, float> measuredAdvances, float[] runAdvances)
        {
            _measuredAdvances = measuredAdvances;
            _runAdvances = runAdvances;
        }

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream) => throw new NotSupportedException();

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        {
            var resolvedText = text ?? string.Empty;
            return
            [
                new TypefaceSpan(resolvedText, GetAdvance(resolvedText), _typeface)
            ];
        }

        public SKFontMetrics GetFontMetrics(SKPaint paint) => default;

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        {
            bounds = default;
            return GetAdvance(text ?? string.Empty);
        }

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y) => null;

        public SKTypeface? FindRunTypeface(string? text, SKPaint paintPreferredTypeface) => _typeface;

        public bool TryShapeGlyphRun(string? text, SKPaint paint, out ShapedGlyphRun shapedRun)
        {
            if (string.IsNullOrEmpty(text) || text.Length != _runAdvances.Length)
            {
                shapedRun = default;
                return false;
            }

            var glyphs = new ushort[_runAdvances.Length];
            var points = new SKPoint[_runAdvances.Length];
            var clusters = new int[_runAdvances.Length];
            var currentX = 0f;

            for (var i = 0; i < _runAdvances.Length; i++)
            {
                glyphs[i] = (ushort)(i + 1);
                points[i] = new SKPoint(currentX, 0f);
                clusters[i] = i;
                currentX += _runAdvances[i];
            }

            shapedRun = new ShapedGlyphRun(glyphs, points, clusters, currentX);
            return true;
        }

        private float GetAdvance(string text)
        {
            return _measuredAdvances.TryGetValue(text, out var advance) ? advance : 0f;
        }
    }
}
