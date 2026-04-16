using System;
using System.Collections;
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
    private static readonly MethodInfo s_measureTextAdvanceMethod = s_svgSceneTextCompilerType.GetMethod(
        "MeasureTextAdvance",
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
    private static readonly MethodInfo s_measureLineStatsMethod = s_svgSceneTextCompilerType.GetMethod(
        "MeasureLineStats",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        [typeof(SvgTextBase), typeof(string), typeof(SKRect), typeof(ISvgAssetLoader)],
        modifiers: null)!;
    private static readonly MethodInfo s_clearPreparedTextCachesMethod = s_svgSceneTextCompilerType.GetMethod("ClearPreparedTextCaches", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo s_naturalTextAdvanceCacheField = s_svgSceneTextCompilerType.GetField("s_naturalTextAdvanceCache", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo s_naturalCodepointAdvanceCacheField = s_svgSceneTextCompilerType.GetField("s_naturalCodepointAdvanceCache", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo s_sharedLineStatsCacheField = s_svgSceneTextCompilerType.GetField("s_sharedLineStatsCache", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo s_tryPrepareFlatTextRunMethod = s_svgSceneTextCompilerType
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(method =>
        {
            if (method.Name != "TryPrepareFlatTextRun")
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 5 &&
                   parameters[0].ParameterType == typeof(SvgTextBase) &&
                   parameters[1].ParameterType == typeof(string);
        });
    private static readonly MethodInfo s_tryPrepareSequentialTextRunsMethod = s_svgSceneTextCompilerType
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(method =>
        {
            if (method.Name != "TryPrepareSequentialTextRuns")
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 5 &&
                   parameters[0].ParameterType == typeof(SvgTextBase);
        });
    private static readonly MethodInfo s_tryCompileSequentialTextMethod = s_svgSceneTextCompilerType.GetMethod(
        "TryCompileSequentialText",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        [typeof(SvgTextBase), typeof(SKRect), typeof(DrawAttributes), typeof(ISvgAssetLoader), typeof(SKRect).MakeByRefType(), typeof(SKPicture).MakeByRefType()],
        modifiers: null)!;

    private sealed record PreparedSequentialRunSnapshot(
        SvgTextBase StyleSource,
        string Text,
        float Advance);

    private sealed record PreparedFlatTextRunSnapshot(
        SvgTextBase StyleSource,
        string Text,
        float Advance,
        float NaturalAdvance,
        IReadOnlyList<float> NaturalCodepointAdvances);

    private sealed record PreparedSequentialTextSnapshot(
        float TotalAdvance,
        IReadOnlyList<PreparedSequentialRunSnapshot> Runs);

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
    public void MeasureNaturalCodepointAdvances_ReusesAcrossFreshLoaders_WhenTextMeasurementCacheKeyMatches()
    {
        ClearPreparedTextCaches();
        try
        {
            var document = CreateDocument("Cache", 24);
            var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
            var geometryBounds = GetDocumentViewport(document);
            var codepoints = InvokeSplitCodepoints("Cache");
            var cacheKey = Guid.NewGuid().GetHashCode();

            var firstLoader = new SharedKeyCountingAssetLoader(cacheKey);
            var firstAdvances = InvokeMeasureNaturalCodepointAdvances(svgText, codepoints, geometryBounds, firstLoader);
            Assert.Equal(1, GetNaturalCodepointAdvanceCacheCount());

            var secondLoader = new SharedKeyCountingAssetLoader(cacheKey);
            var secondAdvances = InvokeMeasureNaturalCodepointAdvances(svgText, codepoints, geometryBounds, secondLoader);

            Assert.Equal(firstAdvances, secondAdvances);
            Assert.Equal(1, GetNaturalCodepointAdvanceCacheCount());
        }
        finally
        {
            ClearPreparedTextCaches();
        }
    }

    [Fact]
    public void MeasureNaturalCodepointAdvances_DoesNotReuseAcrossFreshLoaders_WhenTextMeasurementCacheKeyDiffers()
    {
        ClearPreparedTextCaches();
        try
        {
            var document = CreateDocument("Cache", 24);
            var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
            var geometryBounds = GetDocumentViewport(document);
            var codepoints = InvokeSplitCodepoints("Cache");
            var firstCacheKey = Guid.NewGuid().GetHashCode();

            var firstLoader = new SharedKeyCountingAssetLoader(firstCacheKey);
            _ = InvokeMeasureNaturalCodepointAdvances(svgText, codepoints, geometryBounds, firstLoader);

            var secondLoader = new SharedKeyCountingAssetLoader(unchecked(firstCacheKey + 1));
            _ = InvokeMeasureNaturalCodepointAdvances(svgText, codepoints, geometryBounds, secondLoader);

            Assert.Equal(2, GetNaturalCodepointAdvanceCacheCount());
        }
        finally
        {
            ClearPreparedTextCaches();
        }
    }

    [Fact]
    public void MeasureNaturalTextAdvance_ReusesAcrossFreshLoaders_WhenTextMeasurementCacheKeyMatches()
    {
        ClearPreparedTextCaches();
        try
        {
            var document = CreateDocument("Cache", 24);
            var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
            var geometryBounds = GetDocumentViewport(document);
            var cacheKey = Guid.NewGuid().GetHashCode();

            var firstLoader = new SharedKeyCountingAssetLoader(cacheKey);
            var firstAdvance = InvokeMeasureNaturalTextAdvance(svgText, "Cache", geometryBounds, firstLoader);
            Assert.Equal(1, GetNaturalTextAdvanceCacheCount());

            var secondLoader = new SharedKeyCountingAssetLoader(cacheKey);
            var secondAdvance = InvokeMeasureNaturalTextAdvance(svgText, "Cache", geometryBounds, secondLoader);

            Assert.Equal(firstAdvance, secondAdvance, 3);
            Assert.Equal(1, GetNaturalTextAdvanceCacheCount());
        }
        finally
        {
            ClearPreparedTextCaches();
        }
    }

    [Fact]
    public void MeasureNaturalTextAdvance_DoesNotReuseAcrossFreshLoaders_WhenTextMeasurementCacheKeyDiffers()
    {
        ClearPreparedTextCaches();
        try
        {
            var document = CreateDocument("Cache", 24);
            var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
            var geometryBounds = GetDocumentViewport(document);
            var firstCacheKey = Guid.NewGuid().GetHashCode();

            var firstLoader = new SharedKeyCountingAssetLoader(firstCacheKey);
            _ = InvokeMeasureNaturalTextAdvance(svgText, "Cache", geometryBounds, firstLoader);

            var secondLoader = new SharedKeyCountingAssetLoader(unchecked(firstCacheKey + 1));
            _ = InvokeMeasureNaturalTextAdvance(svgText, "Cache", geometryBounds, secondLoader);

            Assert.Equal(2, GetNaturalTextAdvanceCacheCount());
        }
        finally
        {
            ClearPreparedTextCaches();
        }
    }

    [Fact]
    public void MeasureLineStats_ReusesAcrossFreshLoaders_WhenTextMeasurementCacheKeyMatches()
    {
        ClearPreparedTextCaches();
        try
        {
            var document = CreateDocument("Cache", 24);
            var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
            var geometryBounds = GetDocumentViewport(document);
            var cacheKey = Guid.NewGuid().GetHashCode();

            var firstLoader = new SharedKeyCountingAssetLoader(cacheKey);
            var firstLineStats = InvokeMeasureLineStats(svgText, "Cache", geometryBounds, firstLoader);
            Assert.Equal(1, GetSharedLineStatsCacheCount());

            var secondLoader = new SharedKeyCountingAssetLoader(cacheKey);
            var secondLineStats = InvokeMeasureLineStats(svgText, "Cache", geometryBounds, secondLoader);

            Assert.Same(firstLineStats, secondLineStats);
            Assert.Equal(1, GetSharedLineStatsCacheCount());
        }
        finally
        {
            ClearPreparedTextCaches();
        }
    }

    [Fact]
    public void MeasureLineStats_DoesNotReuseAcrossFreshLoaders_WhenTextMeasurementCacheKeyDiffers()
    {
        ClearPreparedTextCaches();
        try
        {
            var document = CreateDocument("Cache", 24);
            var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
            var geometryBounds = GetDocumentViewport(document);
            var firstCacheKey = Guid.NewGuid().GetHashCode();

            var firstLoader = new SharedKeyCountingAssetLoader(firstCacheKey);
            _ = InvokeMeasureLineStats(svgText, "Cache", geometryBounds, firstLoader);

            var secondLoader = new SharedKeyCountingAssetLoader(unchecked(firstCacheKey + 1));
            _ = InvokeMeasureLineStats(svgText, "Cache", geometryBounds, secondLoader);

            Assert.Equal(2, GetSharedLineStatsCacheCount());
        }
        finally
        {
            ClearPreparedTextCaches();
        }
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

    [Fact]
    public void TryCompileSequentialText_FallsBack_ForSignInAssetTextNodes()
    {
        var document = SvgDocument.Open(GetTestAssetPath("Sign in.svg"));
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings { EnableSvgFonts = true }));
        var compiledIds = new List<string>();

        foreach (var svgText in document.Descendants().OfType<SvgText>())
        {
            if (InvokeTryCompileSequentialText(svgText, viewport, assetLoader))
            {
                compiledIds.Add(svgText.ID ?? svgText.GetType().Name);
            }
        }

        Assert.Empty(compiledIds);
    }

    [Fact]
    public void TryCompileSequentialText_Succeeds_ForDirectedAsciiRuns()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="320" height="120" viewBox="0 0 320 120">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="24" direction="rtl" unicode-bidi="embed">ABC<tspan font-weight="bold">DEF</tspan></text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var succeeded = InvokeTryCompileSequentialText(svgText, viewport, assetLoader);

        Assert.True(succeeded);
    }

    [Fact]
    public void TryCompileSequentialText_FallsBack_ForNestedTextLengthAdjustment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="320" height="120" viewBox="0 0 320 120">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="24">A<tspan textLength="120"><tspan>BC</tspan></tspan>D</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var succeeded = InvokeTryCompileSequentialText(svgText, viewport, assetLoader);

        Assert.False(succeeded);
    }

    [Fact]
    public void TryPrepareFlatTextRun_ReturnsPreparedNaturalMetrics_ForSimpleText()
    {
        var document = CreateDocument("AVATAR ", 24);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var codepoints = InvokeSplitCodepoints("AVATAR ");

        var prepared = InvokeTryPrepareFlatTextRun(svgText, "AVATAR ", geometryBounds, assetLoader, out var snapshot);

        Assert.True(prepared);
        Assert.NotNull(snapshot);
        Assert.Same(svgText, snapshot!.StyleSource);
        Assert.Equal("AVATAR ", snapshot.Text);
        Assert.Equal(InvokeMeasureTextAdvance(svgText, "AVATAR ", geometryBounds, assetLoader), snapshot.Advance, 3);
        Assert.Equal(InvokeMeasureNaturalTextAdvance(svgText, "AVATAR ", geometryBounds, assetLoader), snapshot.NaturalAdvance, 3);

        var expectedCodepointAdvances = InvokeMeasureNaturalCodepointAdvances(svgText, codepoints, geometryBounds, assetLoader);
        Assert.Equal(expectedCodepointAdvances.Length, snapshot.NaturalCodepointAdvances.Count);
        for (var i = 0; i < expectedCodepointAdvances.Length; i++)
        {
            Assert.Equal(expectedCodepointAdvances[i], snapshot.NaturalCodepointAdvances[i], 3);
        }
    }

    [Fact]
    public void TryPrepareSequentialTextRuns_ReturnsPerRunAdvances_ForFlatNestedText()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="320" height="120" viewBox="0 0 320 120">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="24">start<tspan font-weight="bold">Mid</tspan>end</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var prepared = InvokeTryPrepareSequentialTextRuns(svgText, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart: true, out var snapshot);

        Assert.True(prepared);
        Assert.NotNull(snapshot);
        Assert.Collection(
            snapshot!.Runs,
            run => Assert.Equal("start", run.Text),
            run => Assert.Equal("Mid", run.Text),
            run => Assert.Equal("end", run.Text));

        foreach (var run in snapshot.Runs)
        {
            var expectedAdvance = InvokeMeasureTextAdvance(run.StyleSource, run.Text, geometryBounds, assetLoader);
            Assert.Equal(expectedAdvance, run.Advance, 3);
        }

        Assert.Equal(snapshot.Runs.Sum(static run => run.Advance), snapshot.TotalAdvance, 3);
    }

    [Fact]
    public void TryPrepareSequentialTextRuns_ReturnsPerRunAdvances_ForMixedDirectionNestedText()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="320" height="120" viewBox="0 0 320 120">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="24">abc<tspan font-weight="bold">אבג</tspan>xyz</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var prepared = InvokeTryPrepareSequentialTextRuns(svgText, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart: true, out var snapshot);

        Assert.True(prepared);
        Assert.NotNull(snapshot);
        Assert.Collection(
            snapshot!.Runs,
            run => Assert.Equal("abc", run.Text),
            run => Assert.Equal("אבג", run.Text),
            run => Assert.Equal("xyz", run.Text));

        foreach (var run in snapshot.Runs)
        {
            var expectedAdvance = InvokeMeasureTextAdvance(run.StyleSource, run.Text, geometryBounds, assetLoader);
            Assert.Equal(expectedAdvance, run.Advance, 3);
        }
    }

    [Fact]
    public void TryPrepareSequentialTextRuns_RejectsPositionedTspanBarrier()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="24">a<tspan dx="10">b</tspan>c</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var prepared = InvokeTryPrepareSequentialTextRuns(svgText, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart: true, out var snapshot);

        Assert.False(prepared);
        Assert.Null(snapshot);
    }

    [Fact]
    public void TryPrepareSequentialTextRuns_RejectsTextLengthAdjustment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="24" textLength="180">a<tspan>b</tspan>c</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var prepared = InvokeTryPrepareSequentialTextRuns(svgText, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart: true, out var snapshot);

        Assert.False(prepared);
        Assert.Null(snapshot);
    }

    [Fact]
    public void TryPrepareSequentialTextRuns_RejectsNestedTextLengthAdjustmentContainer()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="24">a<tspan textLength="120"><tspan>b</tspan></tspan>c</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var prepared = InvokeTryPrepareSequentialTextRuns(svgText, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart: true, out var snapshot);

        Assert.False(prepared);
        Assert.Null(snapshot);
    }

    [Fact]
    public void TryPrepareSequentialTextRuns_RejectsTextPathBarrier()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="320" height="120" viewBox="0 0 320 120">
              <defs>
                <path id="curve" d="M20 60 C 80 10, 180 10, 260 60" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="24">
                <textPath xlink:href="#curve">Curved text</textPath>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var prepared = InvokeTryPrepareSequentialTextRuns(svgText, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart: true, out var snapshot);

        Assert.False(prepared);
        Assert.Null(snapshot);
    }

    [Fact]
    public void TryPrepareSequentialTextRuns_RejectsSvgFontRun()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="120" viewBox="0 0 240 120">
              <defs>
                <font id="DefaultFontFace" horiz-adv-x="100">
                  <font-face font-family="DefaultFont" units-per-em="100" ascent="100" descent="0" />
                  <glyph unicode="A" horiz-adv-x="100" d="M10 0H30V100H10Z" />
                </font>
              </defs>
              <text id="label" x="10" y="60" font-family="DefaultFont" font-size="48">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings { EnableSvgFonts = true }));

        var prepared = InvokeTryPrepareSequentialTextRuns(svgText, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart: true, out var snapshot);

        Assert.False(prepared);
        Assert.Null(snapshot);
    }

    [Fact]
    public void TryPrepareSequentialTextRuns_DoesNotRequirePrefixMeasurements()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="320" height="120" viewBox="0 0 320 120">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="24">start<tspan font-weight="bold">Mid</tspan>end</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new StrictWholeRunAssetLoader(new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["start"] = 20f,
            ["Mid"] = 12f,
            ["end"] = 14f
        });

        var prepared = InvokeTryPrepareSequentialTextRuns(svgText, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart: true, out var snapshot);

        Assert.True(prepared);
        Assert.NotNull(snapshot);
        Assert.Equal(46f, snapshot!.TotalAdvance, 3);
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

    private static string GetTestAssetPath(string name)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tests", name));
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

    private static float InvokeMeasureTextAdvance(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return Assert.IsType<float>(s_measureTextAdvanceMethod.Invoke(null, [svgTextBase, text, geometryBounds, assetLoader]));
    }

    private static float[] InvokeMeasureNaturalCodepointAdvances(
        SvgTextBase svgTextBase,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return Assert.IsType<float[]>(s_measureNaturalCodepointAdvancesMethod.Invoke(null, [svgTextBase, codepoints, geometryBounds, assetLoader]));
    }

    private static object InvokeMeasureLineStats(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return s_measureLineStatsMethod.Invoke(null, [svgTextBase, text, geometryBounds, assetLoader])
            ?? throw new InvalidOperationException("MeasureLineStats returned null.");
    }

    private static void ClearPreparedTextCaches()
    {
        s_clearPreparedTextCachesMethod.Invoke(null, null);
    }

    private static int GetNaturalCodepointAdvanceCacheCount()
    {
        var cache = s_naturalCodepointAdvanceCacheField.GetValue(null)
            ?? throw new InvalidOperationException("Natural codepoint advance cache was not initialized.");
        return Assert.IsType<int>(cache.GetType().GetProperty("Count")!.GetValue(cache));
    }

    private static int GetNaturalTextAdvanceCacheCount()
    {
        var cache = s_naturalTextAdvanceCacheField.GetValue(null)
            ?? throw new InvalidOperationException("Natural text advance cache was not initialized.");
        return Assert.IsType<int>(cache.GetType().GetProperty("Count")!.GetValue(cache));
    }

    private static int GetSharedLineStatsCacheCount()
    {
        var cache = s_sharedLineStatsCacheField.GetValue(null)
            ?? throw new InvalidOperationException("Shared line stats cache was not initialized.");
        return Assert.IsType<int>(cache.GetType().GetProperty("Count")!.GetValue(cache));
    }

    private static bool InvokeTryPrepareFlatTextRun(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out PreparedFlatTextRunSnapshot? snapshot)
    {
        var args = new object?[]
        {
            svgTextBase,
            text,
            geometryBounds,
            assetLoader,
            null
        };

        var prepared = Assert.IsType<bool>(s_tryPrepareFlatTextRunMethod.Invoke(null, args));
        if (!prepared)
        {
            snapshot = null;
            return false;
        }

        Assert.NotNull(args[4]);
        var preparedText = args[4]!;
        var preparedType = preparedText.GetType();
        var naturalCodepointAdvances = Assert.IsAssignableFrom<IEnumerable>(preparedType.GetProperty("NaturalCodepointAdvances")!.GetValue(preparedText))
            .Cast<object>()
            .Select(static value => Assert.IsType<float>(value))
            .ToArray();
        snapshot = new PreparedFlatTextRunSnapshot(
            Assert.IsAssignableFrom<SvgTextBase>(preparedType.GetProperty("StyleSource")!.GetValue(preparedText)),
            Assert.IsType<string>(preparedType.GetProperty("Text")!.GetValue(preparedText)),
            Assert.IsType<float>(preparedType.GetProperty("Advance")!.GetValue(preparedText)),
            Assert.IsType<float>(preparedType.GetProperty("NaturalAdvance")!.GetValue(preparedText)),
            naturalCodepointAdvances);
        return true;
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

    private static bool InvokeTryPrepareSequentialTextRuns(
        SvgTextBase svgTextBase,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool trimLeadingWhitespaceAtStart,
        out PreparedSequentialTextSnapshot? snapshot)
    {
        var args = new object?[]
        {
            svgTextBase,
            geometryBounds,
            assetLoader,
            trimLeadingWhitespaceAtStart,
            null
        };

        var prepared = Assert.IsType<bool>(s_tryPrepareSequentialTextRunsMethod.Invoke(null, args));
        if (!prepared)
        {
            snapshot = null;
            return false;
        }

        Assert.NotNull(args[4]);
        var preparedText = args[4]!;
        var preparedType = preparedText.GetType();
        var totalAdvance = Assert.IsType<float>(preparedType.GetProperty("TotalAdvance")!.GetValue(preparedText));
        var runsValue = Assert.IsAssignableFrom<IEnumerable>(preparedType.GetProperty("Runs")!.GetValue(preparedText));
        var runs = new List<PreparedSequentialRunSnapshot>();

        foreach (var runValue in runsValue)
        {
            Assert.NotNull(runValue);
            var run = runValue!;
            var runType = run.GetType();
            runs.Add(new PreparedSequentialRunSnapshot(
                Assert.IsAssignableFrom<SvgTextBase>(runType.GetProperty("StyleSource")!.GetValue(run)),
                Assert.IsType<string>(runType.GetProperty("Text")!.GetValue(run)),
                Assert.IsType<float>(runType.GetProperty("Advance")!.GetValue(run))));
        }

        snapshot = new PreparedSequentialTextSnapshot(totalAdvance, runs);
        return true;
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

    private sealed class StrictWholeRunAssetLoader : ISvgAssetLoader, ISvgTextRunTypefaceResolver
    {
        private readonly IReadOnlyDictionary<string, float> _measuredAdvances;
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public StrictWholeRunAssetLoader(IReadOnlyDictionary<string, float> measuredAdvances)
        {
            _measuredAdvances = measuredAdvances;
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

        private float GetAdvance(string text)
        {
            if (_measuredAdvances.TryGetValue(text, out var advance))
            {
                return advance;
            }

            throw new InvalidOperationException($"Unexpected prefix measurement for '{text}'.");
        }
    }

    private sealed class SharedKeyCountingAssetLoader : ISvgAssetLoader, ISvgTextMeasurementCacheKeyProvider, ISvgTextRunTypefaceResolver
    {
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public SharedKeyCountingAssetLoader(int cacheKey)
        {
            TextMeasurementCacheKey = cacheKey;
        }

        public bool EnableSvgFonts => false;

        public int TextMeasurementCacheKey { get; }

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

        private static float GetAdvance(string text)
        {
            return text.Length * 10f;
        }
    }
}
