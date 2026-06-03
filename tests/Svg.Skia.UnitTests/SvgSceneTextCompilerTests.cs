using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
    private static readonly Type s_svgSceneContextPaintType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgSceneContextPaint")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgSceneContextPaint.");
    private static readonly Type s_svgFontTextRendererType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgFontTextRenderer")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgFontTextRenderer.");
    private static readonly Type s_svgTextLayoutPlannerType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextLayoutPlanner")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextLayoutPlanner.");
    private static readonly Type s_svgTextBoundaryResolverType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextBoundaryResolver")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextBoundaryResolver.");
    private static readonly Type s_svgTextPathLayoutPlannerType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextPathLayoutPlanner")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextPathLayoutPlanner.");
    private static readonly Type s_svgTextPathLayoutPlannerPathSampleType =
        s_svgTextPathLayoutPlannerType.GetNestedType("PathSample", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.PathSample.");
    private static readonly Type s_svgTextPathLayoutPlannerStretchClusterInputType =
        s_svgTextPathLayoutPlannerType.GetNestedType("StretchClusterInput", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.StretchClusterInput.");
    private static readonly Type s_svgSceneTextCompilerPathSampleType =
        s_svgSceneTextCompilerType.GetNestedType("PathSample", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not locate SvgSceneTextCompiler.PathSample.");
    private static readonly Type s_svgTextDirectionType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextDirection")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextDirection.");
    private static readonly Type s_svgUnicodeBidiModeType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgUnicodeBidiMode")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgUnicodeBidiMode.");
    private static readonly Type s_svgTextLineBreakOptionsType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextLineBreakOptions")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextLineBreakOptions.");
    private static readonly Type s_svgTextLayoutStyleType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextLayoutStyle")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextLayoutStyle.");
    private static readonly Type s_svgTextLayoutInputRunType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextLayoutInputRun")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextLayoutInputRun.");
    private static readonly Type s_svgCssShapeImageSamplerType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgCssShapeImageSampler")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgCssShapeImageSampler.");

    private static readonly MethodInfo s_splitCodepointsMethod = s_svgSceneTextCompilerType.GetMethod("SplitCodepoints", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo s_createTextLayoutPlanMethod = s_svgTextLayoutPlannerType
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
        {
            if (method.Name != "Create")
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 5 &&
                   parameters[0].ParameterType == typeof(string);
        });
    private static readonly MethodInfo s_createTextLayoutPlanFromRunsMethod = s_svgTextLayoutPlannerType
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
        {
            if (method.Name != "Create")
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 2 &&
                   parameters[0].ParameterType.IsGenericType &&
                   parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);
        });
    private static readonly MethodInfo s_createLineScopedVisualRunsMethod = s_svgTextLayoutPlannerType
        .GetMethod(
            "CreateLineScopedVisualRuns",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [s_svgTextLayoutPlannerType.Assembly.GetType("Svg.Skia.SvgTextLayoutPlan")!, typeof(int), typeof(int)],
            modifiers: null)!;
    private static readonly object s_textBoundaryResolver =
        s_svgTextBoundaryResolverType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)
        ?? throw new InvalidOperationException("Could not locate SvgTextBoundaryResolver.Default.");
    private static readonly MethodInfo s_getGraphemeClusterStartCharIndexesMethod =
        s_svgTextBoundaryResolverType.GetMethod(
            "GetGraphemeClusterStartCharIndexes",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            [typeof(string)],
            modifiers: null)
        ?? throw new InvalidOperationException("Could not locate SvgTextBoundaryResolver.GetGraphemeClusterStartCharIndexes.");
    private static readonly MethodInfo s_tryGetTextPathPlannerPointAndTangentMethod = s_svgTextPathLayoutPlannerType
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(method =>
        {
            if (method.Name != "TryGetPointAndTangent")
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 4 &&
                   parameters[1].ParameterType == typeof(float);
        });
    private static readonly MethodInfo s_tryCreateStretchClusterPlanMethod = s_svgTextPathLayoutPlannerType
        .GetMethod("TryCreateStretchClusterPlan", BindingFlags.NonPublic | BindingFlags.Static)!;
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
    private static readonly MethodInfo s_drawTextRunsMethod = s_svgSceneTextCompilerType.GetMethod(
        "DrawTextRuns",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        [typeof(SvgTextBase), typeof(string), typeof(float), typeof(float), typeof(SKRect), typeof(SKPaint), typeof(SKCanvas), typeof(ISvgAssetLoader), typeof(float[])],
        modifiers: null)!;
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
    private static readonly MethodInfo s_tryCreateTextContentMetricsMethod = s_svgSceneTextCompilerType.GetMethod(
        "TryCreateTextContentMetrics",
        BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo s_tryCreateSharedInlineSizeTextLayoutResultMethod = s_svgSceneTextCompilerType.GetMethod(
        "TryCreateSharedInlineSizeTextLayoutResult",
        BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo s_tryCreateCssShapeAlphaPathMethod = s_svgCssShapeImageSamplerType
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
        {
            if (method.Name != "TryCreateAlphaPath")
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 4 &&
                   parameters[0].ParameterType == typeof(byte[]);
        });
    private static readonly MethodInfo s_tryCreateStretchedTextPathClustersMethod = s_svgSceneTextCompilerType.GetMethod(
        "TryCreateStretchedTextPathClusters",
        BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo s_tryCreateStretchedTextPathRunPathMethod = s_svgSceneTextCompilerType.GetMethod(
        "TryCreateStretchedTextPathRunPath",
        BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo s_tryCompileSequentialTextMethod = s_svgSceneTextCompilerType.GetMethod(
        "TryCompileSequentialText",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        [typeof(SvgTextBase), typeof(SKRect), typeof(DrawAttributes), typeof(ISvgAssetLoader), typeof(Func<SvgElement?, string?>), s_svgSceneContextPaintType, typeof(SKRect).MakeByRefType(), typeof(SKPicture).MakeByRefType()],
        modifiers: null)!;
    private static readonly MethodInfo s_tryGetSvgFontLayoutMethod = s_svgFontTextRendererType
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(method => method.Name == "TryGetLayout" && method.GetParameters().Length == 6);

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

    private sealed class TextContentMetricsSnapshot
    {
        private readonly object _metrics;
        private readonly MethodInfo _getStartPositionOfChar;
        private readonly MethodInfo _getEndPositionOfChar;
        private readonly MethodInfo _getExtentOfChar;
        private readonly MethodInfo _getSubStringLength;
        private readonly MethodInfo _getCharNumAtPosition;
        private readonly MethodInfo _getRotationOfChar;

        public TextContentMetricsSnapshot(object metrics)
        {
            _metrics = metrics;
            var metricsType = metrics.GetType();
            NumberOfChars = Assert.IsType<int>(metricsType.GetProperty("NumberOfChars")!.GetValue(metrics));
            ComputedTextLength = Assert.IsType<float>(metricsType.GetProperty("ComputedTextLength")!.GetValue(metrics));
            _getStartPositionOfChar = metricsType.GetMethod("GetStartPositionOfChar")!;
            _getEndPositionOfChar = metricsType.GetMethod("GetEndPositionOfChar")!;
            _getExtentOfChar = metricsType.GetMethod("GetExtentOfChar")!;
            _getSubStringLength = metricsType.GetMethod("GetSubStringLength")!;
            _getCharNumAtPosition = metricsType.GetMethod("GetCharNumAtPosition")!;
            _getRotationOfChar = metricsType.GetMethod("GetRotationOfChar")!;
        }

        public int NumberOfChars { get; }

        public float ComputedTextLength { get; }

        public SKPoint FirstStartPosition => NumberOfChars > 0 ? GetStartPositionOfChar(0) : default;

        public SKPoint LastEndPosition => NumberOfChars > 0 ? GetEndPositionOfChar(NumberOfChars - 1) : default;

        public float FirstCharLength => NumberOfChars > 0 ? GetSubStringLength(0, 1) : 0f;

        public float FullSubstringLength => NumberOfChars > 0 ? GetSubStringLength(0, NumberOfChars) : 0f;

        public SKPoint GetStartPositionOfChar(int charnum)
        {
            return Assert.IsType<SKPoint>(_getStartPositionOfChar.Invoke(_metrics, [charnum]));
        }

        public SKPoint GetEndPositionOfChar(int charnum)
        {
            return Assert.IsType<SKPoint>(_getEndPositionOfChar.Invoke(_metrics, [charnum]));
        }

        public SKRect GetExtentOfChar(int charnum)
        {
            return Assert.IsType<SKRect>(_getExtentOfChar.Invoke(_metrics, [charnum]));
        }

        public float GetSubStringLength(int charnum, int nchars)
        {
            return Assert.IsType<float>(_getSubStringLength.Invoke(_metrics, [charnum, nchars]));
        }

        public int GetCharNumAtPosition(SKPoint point)
        {
            return Assert.IsType<int>(_getCharNumAtPosition.Invoke(_metrics, [point]));
        }

        public float GetRotationOfChar(int charnum)
        {
            return Assert.IsType<float>(_getRotationOfChar.Invoke(_metrics, [charnum]));
        }
    }

    private sealed class SharedTextLayoutSnapshot
    {
        private readonly object _domMetrics;
        private readonly MethodInfo _getDomSubStringLength;

        public SharedTextLayoutSnapshot(object result, float finalX, float finalY)
        {
            var resultType = result.GetType();
            ComputedTextLength = Assert.IsType<float>(resultType.GetProperty("ComputedTextLength")!.GetValue(result));
            Bounds = Assert.IsType<SKRect>(resultType.GetProperty("Bounds")!.GetValue(result));
            FinalX = finalX;
            FinalY = finalY;
            Lines = Assert.IsAssignableFrom<IEnumerable>(resultType.GetProperty("Lines")!.GetValue(result))
                .Cast<object>()
                .Select(static line => new SharedTextLineSnapshot(line))
                .ToArray();

            var domMetrics = resultType.GetProperty("DomMetrics")!.GetValue(result);
            Assert.NotNull(domMetrics);
            _domMetrics = domMetrics!;
            var domMetricsType = domMetrics!.GetType();
            _getDomSubStringLength = domMetricsType.GetMethod("GetSubStringLength")!;
            DomNumberOfChars = Assert.IsType<int>(domMetricsType.GetProperty("NumberOfChars")!.GetValue(domMetrics));
            DomComputedTextLength = Assert.IsType<float>(domMetricsType.GetProperty("ComputedTextLength")!.GetValue(domMetrics));
            DomClusters = Assert.IsAssignableFrom<IEnumerable>(domMetricsType.GetProperty("Clusters")!.GetValue(domMetrics))
                .Cast<object>()
                .Select(static cluster => new SharedTextDomClusterSnapshot(cluster))
                .ToArray();
        }

        public float ComputedTextLength { get; }

        public SKRect Bounds { get; }

        public float FinalX { get; }

        public float FinalY { get; }

        public IReadOnlyList<SharedTextLineSnapshot> Lines { get; }

        public int DomNumberOfChars { get; }

        public float DomComputedTextLength { get; }

        public IReadOnlyList<SharedTextDomClusterSnapshot> DomClusters { get; }

        public float GetDomSubStringLength(int charnum, int nchars)
        {
            return Assert.IsType<float>(_getDomSubStringLength.Invoke(_domMetrics, [charnum, nchars]));
        }
    }

    private sealed class SharedTextLineSnapshot
    {
        public SharedTextLineSnapshot(object line)
        {
            var lineType = line.GetType();
            LineIndex = Assert.IsType<int>(lineType.GetProperty("LineIndex")!.GetValue(line));
            Flow = lineType.GetProperty("Flow")!.GetValue(line)!.ToString()!;
            BaselineOrigin = Assert.IsType<SKPoint>(lineType.GetProperty("BaselineOrigin")!.GetValue(line));
            InlineStart = Assert.IsType<float>(lineType.GetProperty("InlineStart")!.GetValue(line));
            InlineSize = Assert.IsType<float>(lineType.GetProperty("InlineSize")!.GetValue(line));
            Advance = Assert.IsType<float>(lineType.GetProperty("Advance")!.GetValue(line));
            InlineProgression = Assert.IsType<int>(lineType.GetProperty("InlineProgression")!.GetValue(line));
            BaselineOffset = Assert.IsType<float>(lineType.GetProperty("BaselineOffset")!.GetValue(line));
            PositionedSpans = Assert.IsAssignableFrom<IEnumerable>(lineType.GetProperty("PositionedSpans")!.GetValue(line))
                .Cast<object>()
                .Select(static span => new SharedTextPositionedSpanSnapshot(span))
                .ToArray();
        }

        public int LineIndex { get; }

        public string Flow { get; }

        public SKPoint BaselineOrigin { get; }

        public float InlineStart { get; }

        public float InlineSize { get; }

        public float Advance { get; }

        public int InlineProgression { get; }

        public float BaselineOffset { get; }

        public IReadOnlyList<SharedTextPositionedSpanSnapshot> PositionedSpans { get; }
    }

    private sealed class SharedTextPositionedSpanSnapshot
    {
        public SharedTextPositionedSpanSnapshot(object span)
        {
            var spanType = span.GetType();
            Text = Assert.IsType<string>(spanType.GetProperty("Text")!.GetValue(span));
            Advance = Assert.IsType<float>(spanType.GetProperty("Advance")!.GetValue(span));
            Bounds = Assert.IsType<SKRect>(spanType.GetProperty("Bounds")!.GetValue(span));
            TextLengthSource = spanType.GetProperty("TextLengthSource")!.GetValue(span) as SvgTextBase;
            NaturalAdvance = Assert.IsType<float>(spanType.GetProperty("NaturalAdvance")!.GetValue(span));
            AppliedTextLength = Assert.IsType<float>(spanType.GetProperty("AppliedTextLength")!.GetValue(span));
            BaselineOrigin = Assert.IsType<SKPoint>(spanType.GetProperty("BaselineOrigin")!.GetValue(span));
            Placements = Assert.IsAssignableFrom<IEnumerable>(spanType.GetProperty("Placements")!.GetValue(span))
                .Cast<object>()
                .Select(static placement => new SharedTextPlacementSnapshot(placement))
                .ToArray();
        }

        public string Text { get; }

        public float Advance { get; }

        public SKRect Bounds { get; }

        public SvgTextBase? TextLengthSource { get; }

        public float NaturalAdvance { get; }

        public float AppliedTextLength { get; }

        public SKPoint BaselineOrigin { get; }

        public IReadOnlyList<SharedTextPlacementSnapshot> Placements { get; }
    }

    private sealed class SharedTextPlacementSnapshot
    {
        public SharedTextPlacementSnapshot(object placement)
        {
            var placementType = placement.GetType();
            Point = Assert.IsType<SKPoint>(placementType.GetProperty("Point")!.GetValue(placement));
            RotationDegrees = Assert.IsType<float>(placementType.GetProperty("RotationDegrees")!.GetValue(placement));
            ScaleX = Assert.IsType<float>(placementType.GetProperty("ScaleX")!.GetValue(placement));
            InlineOffset = Assert.IsType<float>(placementType.GetProperty("InlineOffset")!.GetValue(placement));
            Advance = Assert.IsType<float>(placementType.GetProperty("Advance")!.GetValue(placement));
            CodepointIndex = Assert.IsType<int>(placementType.GetProperty("CodepointIndex")!.GetValue(placement));
            Kind = placementType.GetProperty("Kind")!.GetValue(placement)!.ToString()!;
        }

        public SKPoint Point { get; }

        public float RotationDegrees { get; }

        public float ScaleX { get; }

        public float InlineOffset { get; }

        public float Advance { get; }

        public int CodepointIndex { get; }

        public string Kind { get; }
    }

    private sealed class SharedTextDomClusterSnapshot
    {
        public SharedTextDomClusterSnapshot(object cluster)
        {
            var clusterType = cluster.GetType();
            StartCharIndex = Assert.IsType<int>(clusterType.GetProperty("StartCharIndex")!.GetValue(cluster));
            CharLength = Assert.IsType<int>(clusterType.GetProperty("CharLength")!.GetValue(cluster));
            StartOffset = Assert.IsType<float>(clusterType.GetProperty("StartOffset")!.GetValue(cluster));
            EndOffset = Assert.IsType<float>(clusterType.GetProperty("EndOffset")!.GetValue(cluster));
            StartPoint = Assert.IsType<SKPoint>(clusterType.GetProperty("StartPoint")!.GetValue(cluster));
            EndPoint = Assert.IsType<SKPoint>(clusterType.GetProperty("EndPoint")!.GetValue(cluster));
            Extent = Assert.IsType<SKRect>(clusterType.GetProperty("Extent")!.GetValue(cluster));
            RotationDegrees = Assert.IsType<float>(clusterType.GetProperty("RotationDegrees")!.GetValue(cluster));
        }

        public int StartCharIndex { get; }

        public int CharLength { get; }

        public float StartOffset { get; }

        public float EndOffset { get; }

        public SKPoint StartPoint { get; }

        public SKPoint EndPoint { get; }

        public SKRect Extent { get; }

        public float RotationDegrees { get; }
    }

    [Fact]
    public void MeasureNaturalCodepointAdvances_SimpleAsciiText_MatchesPrefixMeasurement()
    {
        VerifyMatchesPrefixMeasurement("Item 42 ");
    }

    [Fact]
    public void MeasureNaturalCodepointAdvances_CombiningMarkText_PreservesTotalAdvance()
    {
        VerifyPreservesTotalAdvance("Cafe\u0301 ");
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
    public void MeasureNaturalTextAdvance_ReusesCachedRunForSameStyle()
    {
        var document = CreateDocument("Cache", 24);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new CountingNaturalAdvanceAssetLoader();

        var firstAdvance = InvokeMeasureNaturalTextAdvance(svgText, "Cache", geometryBounds, assetLoader);
        var secondAdvance = InvokeMeasureNaturalTextAdvance(svgText, "Cache", geometryBounds, assetLoader);

        Assert.Equal(firstAdvance, secondAdvance);
        Assert.Equal(1, assetLoader.FindTypefacesCallCount);
    }

    [Fact]
    public void MeasureNaturalTextAdvance_RecomputesForDifferentFontSizes_OnSharedAssetLoader()
    {
        var assetLoader = new CountingNaturalAdvanceAssetLoader();
        var smallDocument = CreateDocument("Scale", 12);
        var largeDocument = CreateDocument("Scale", 36);
        var smallText = smallDocument.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var largeText = largeDocument.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var smallBounds = GetDocumentViewport(smallDocument);
        var largeBounds = GetDocumentViewport(largeDocument);

        var smallAdvance = InvokeMeasureNaturalTextAdvance(smallText, "Scale", smallBounds, assetLoader);
        var largeAdvance = InvokeMeasureNaturalTextAdvance(largeText, "Scale", largeBounds, assetLoader);

        Assert.True(largeAdvance > smallAdvance * 2f);
        Assert.Equal(2, assetLoader.FindTypefacesCallCount);
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
    public void TryCreateTextContentMetrics_AltGlyphDefUsesReferencedSvgFontGlyph()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <font id="AltFont" horiz-adv-x="10">
                  <font-face font-family="AltFont" units-per-em="10" ascent="10" descent="0" alphabetic="0" />
                  <glyph id="normal-a" unicode="A" horiz-adv-x="10" d="M0 0H10V10H0Z" />
                  <glyph id="wide-a" glyph-name="wideA" horiz-adv-x="20" d="M0 0H20V10H0Z" />
                </font>
                <altGlyphDef id="wideDef">
                  <glyphRef xlink:href="#wide-a" />
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="40" font-family="AltFont" font-size="10"><altGlyph xlink:href="#wideDef">A</altGlyph></text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings { EnableSvgFonts = true }));

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.Equal(20f, metrics.ComputedTextLength, 3);
        Assert.Equal(20f, metrics.FirstCharLength, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_AltGlyphItemSkipsInvalidCandidateAndUsesGlyphNames()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <font id="AltFont" horiz-adv-x="8">
                  <font-face font-family="AltFont" units-per-em="10" ascent="10" descent="0" alphabetic="0" />
                  <glyph unicode="B" horiz-adv-x="8" d="M0 0H8V10H0Z" />
                  <glyph glyph-name="leftHalf" horiz-adv-x="10" d="M0 0H10V10H0Z" />
                  <glyph glyph-name="rightHalf" horiz-adv-x="12" d="M0 0H12V10H0Z" />
                </font>
                <altGlyphDef id="pairDef">
                  <altGlyphItem>
                    <glyphRef xlink:href="#missing-glyph" />
                  </altGlyphItem>
                  <altGlyphItem>
                    <glyphRef glyphRef="leftHalf" />
                    <glyphRef glyphRef="rightHalf" />
                  </altGlyphItem>
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="40" font-family="AltFont" font-size="10"><altGlyph xlink:href="#pairDef">B</altGlyph></text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings { EnableSvgFonts = true }));

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.Equal(22f, metrics.ComputedTextLength, 3);
        Assert.Equal(22f, metrics.FirstCharLength, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InvalidAltGlyphFallsBackToOriginalText()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <font id="AltFont" horiz-adv-x="10">
                  <font-face font-family="AltFont" units-per-em="10" ascent="10" descent="0" alphabetic="0" />
                  <glyph unicode="A" horiz-adv-x="10" d="M0 0H10V10H0Z" />
                </font>
                <altGlyphDef id="badDef">
                  <glyphRef xlink:href="#missing-glyph" />
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="40" font-family="AltFont" font-size="10"><altGlyph xlink:href="#badDef">A</altGlyph></text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings { EnableSvgFonts = true }));

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.Equal(10f, metrics.ComputedTextLength, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_SvgFontMixedScriptBaselinesUseInheritedTableSize()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="150" viewBox="0 0 260 150">
              <defs>
                <font id="BaseFont" horiz-adv-x="10">
                  <font-face font-family="BaseFont" units-per-em="10" ascent="8" descent="-2" alphabetic="0" ideographic="-2" hanging="8" />
                  <glyph unicode="a" horiz-adv-x="10" d="M0 -0.2H10V0.2H0Z" />
                  <glyph unicode="&#x729C;" horiz-adv-x="10" d="M0 -2.2H10V-1.8H0Z" />
                  <glyph unicode="&#x923;" horiz-adv-x="10" d="M0 7.8H10V8.2H0Z" />
                </font>
              </defs>
              <text id="label" x="10" y="100" font-family="BaseFont" font-size="100">a&#x729C;&#x923;<tspan font-size="50">a&#x729C;&#x923;</tspan></text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var tspan = document.Descendants().OfType<SvgTextSpan>().Single(static element => element.Parent is SvgText);
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings { EnableSvgFonts = true }));

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(6, metrics!.NumberOfChars);

        var largeIdeographicExtent = InvokeSvgFontLayoutBounds(svgText, "\u729C", 100f, 100f);
        var smallIdeographicExtent = InvokeSvgFontLayoutBounds(tspan, "\u729C", 50f, 100f);
        var largeHangingExtent = InvokeSvgFontLayoutBounds(svgText, "\u0923", 100f, 100f);
        var smallHangingExtent = InvokeSvgFontLayoutBounds(tspan, "\u0923", 50f, 100f);
        Assert.Equal(GetVerticalCenter(largeIdeographicExtent), GetVerticalCenter(smallIdeographicExtent), 1);
        Assert.Equal(GetVerticalCenter(largeHangingExtent), GetVerticalCenter(smallHangingExtent), 1);
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

    [Fact]
    public void TryCreateTextContentMetrics_IncludesResolvedTrefContent()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="240" height="80" viewBox="0 0 240 80">
              <defs>
                <text id="source">Hello</text>
              </defs>
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="24">A<tref xlink:href="#source" />B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(7, metrics!.NumberOfChars);
        Assert.True(metrics.ComputedTextLength > 0f);
        Assert.True(metrics.FullSubstringLength > 0f);
        Assert.Equal(metrics.ComputedTextLength, metrics.FullSubstringLength, 3);
        Assert.True(metrics.LastEndPosition.X > metrics.FirstStartPosition.X);
        Assert.True(metrics.FirstCharLength > 0f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_VerticalWritingModeReportsVerticalPositions()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="160" viewBox="0 0 120 160">
              <text id="label" x="50" y="30" font-family="sans-serif" font-size="10" writing-mode="tb">AB</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(20f, metrics.ComputedTextLength, 3);
        Assert.Equal(20f, metrics.FullSubstringLength, 3);
        Assert.Equal(metrics.FirstStartPosition.X, metrics.LastEndPosition.X, 3);
        Assert.True(metrics.LastEndPosition.Y > metrics.FirstStartPosition.Y + 15f);
        Assert.Equal(-90f, metrics.GetRotationOfChar(0), 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_VerticalRlDirectionRtlReportsBottomToTopPositions()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="160" viewBox="0 0 120 160">
              <text id="label" x="50" y="80" font-family="sans-serif" font-size="10" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed">&#x4E00;&#x4E8C;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(firstStart.X, secondStart.X, 3);
        Assert.Equal(50f, firstStart.X, 3);
        Assert.True(secondStart.Y < firstStart.Y - 8f, $"Expected vertical-rl direction=rtl text to advance bottom-to-top, but Y values were {firstStart.Y} and {secondStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeWrapsWordsIntoLines()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">A B C</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);
        Assert.Equal(30f, metrics.ComputedTextLength, 3);
        Assert.Equal(10f, metrics.GetSubStringLength(1, 1), 3);
        Assert.Equal(20f, metrics.GetSubStringLength(1, 2), 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        var thirdStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.Equal(10f, thirdStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 10f);
        Assert.True(thirdStart.Y > secondStart.Y + 10f);
        Assert.Equal(20f, metrics.LastEndPosition.X, 3);
        Assert.Equal(thirdStart.Y, metrics.LastEndPosition.Y, 3);

        var secondExtent = metrics.GetExtentOfChar(1);
        var secondHitPoint = new SKPoint(
            (secondExtent.Left + secondExtent.Right) * 0.5f,
            (secondExtent.Top + secondExtent.Bottom) * 0.5f);
        Assert.Equal(1, metrics.GetCharNumAtPosition(secondHitPoint));
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextPathOnlyUsesContentStart()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="90" y="45" font-family="sans-serif" font-size="10" inline-size="100" text-anchor="middle">
                <textPath id="path-run" href="#line">AB</textPath>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(20f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(40f, firstStart.X, 3);
        Assert.True(secondStart.X > firstStart.X + 5f, $"Expected textPath DOM metrics to advance along the inline-size content area, but X values were {firstStart.X} and {secondStart.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextPathUsesTextPathOwnedTextLength()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="10" y="45" font-family="sans-serif" font-size="10" inline-size="100">
                <textPath id="path-run" href="#line" textLength="80">AB</textPath>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(80f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.True(secondStart.X > firstStart.X + 60f, $"Expected textPath-owned textLength to widen inline-size textPath placement, but X values were {firstStart.X} and {secondStart.X}.");
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextPathRootDxOffsetsContentStart()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="90" y="45" dx="10" font-family="sans-serif" font-size="10" inline-size="100" text-anchor="middle">
                <textPath id="path-run" href="#line">AB</textPath>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        Assert.Equal(50f, firstStart.X, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextPathAllowsStyledWrapper()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="10" y="45" font-family="sans-serif" font-size="10" inline-size="100">
                <tspan fill="red"><textPath id="path-run" href="#line">AB</textPath></tspan>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(20f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.True(secondStart.X > firstStart.X + 5f, $"Expected nested textPath content to keep inline-size path placement, but X values were {firstStart.X} and {secondStart.X}.");
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeMixedTextAndTextPathSiblingsWrap()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="120" viewBox="0 0 240 120">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="10" y="45" font-family="sans-serif" font-size="10" inline-size="35">
                <tspan id="head">AA</tspan><textPath id="path-run" href="#line">A</textPath><tspan id="tail">BB</tspan>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(5, metrics!.NumberOfChars);

        var firstPlain = metrics.GetStartPositionOfChar(0);
        var textPathStart = metrics.GetStartPositionOfChar(2);
        var wrappedTail = metrics.GetStartPositionOfChar(3);
        Assert.Equal(10f, firstPlain.X, 3);
        Assert.True(textPathStart.X > firstPlain.X + 15f, $"Expected textPath sibling to keep its inline slot after plain text, but positions were {firstPlain} and {textPathStart}.");
        Assert.True(wrappedTail.Y > firstPlain.Y + 8f, $"Expected trailing text sibling to wrap after the atomic textPath segment, but positions were {firstPlain}, {textPathStart}, and {wrappedTail}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeMixedTextPathBenchmarkSceneUsesSkiaLoader()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="920" height="380" viewBox="0 0 920 380">
              <defs>
                <path id="wideArc" d="M40 286 C220 206 520 206 900 286" />
              </defs>
              <g font-family="Noto Sans, Arial, sans-serif" font-size="20" fill="#111827">
                <text id="inline-mixed-text-path" x="520" y="286" inline-size="62"><tspan id="mixed-path-head">AA</tspan><textPath id="inline-mixed-path-run" href="#wideArc" startOffset="0%">A</textPath><tspan id="mixed-path-tail">BB</tspan></text>
              </g>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "inline-mixed-text-path");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(5, metrics!.NumberOfChars);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeWrappedTextPathUsesRootTextLength()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="10" y="45" font-family="sans-serif" font-size="10" inline-size="120" textLength="80">
                <textPath id="path-run" href="#line">AB</textPath>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(80f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.True(secondStart.X > firstStart.X + 60f, $"Expected root textLength to stretch inline-size textPath placement, but X values were {firstStart.X} and {secondStart.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeMixedTextPathUsesRootTextLength()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="10" y="45" font-family="sans-serif" font-size="10" inline-size="150" textLength="120">
                <tspan id="head">AA</tspan><textPath id="path-run" href="#line">A</textPath><tspan id="tail">B</tspan>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);
        Assert.Equal(120f, metrics.ComputedTextLength, 3);

        var firstPlain = metrics.GetStartPositionOfChar(0);
        var textPathStart = metrics.GetStartPositionOfChar(2);
        var tailStart = metrics.GetStartPositionOfChar(3);
        Assert.Equal(10f, firstPlain.X, 3);
        Assert.True(textPathStart.X > firstPlain.X + 55f, $"Expected root textLength to reserve an adjusted slot before the textPath sibling, but positions were {firstPlain}, {textPathStart}, and {tailStart}.");
        Assert.True(tailStart.X > textPathStart.X + 20f, $"Expected root textLength to reserve an adjusted slot after the textPath sibling, but positions were {firstPlain}, {textPathStart}, and {tailStart}.");
        Assert.Equal(firstPlain.Y, textPathStart.Y, 3);
        Assert.Equal(firstPlain.Y, tailStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeNestedWrapperTextPathWrapsWithSiblings()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="120" viewBox="0 0 240 120">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="10" y="45" font-family="sans-serif" font-size="10" inline-size="15">
                <tspan fill="red"><textPath id="path-run" href="#line">A</textPath></tspan><tspan id="tail">B</tspan>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var textPathStart = metrics.GetStartPositionOfChar(0);
        var tailStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, textPathStart.X, 3);
        Assert.Equal(10f, tailStart.X, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeRightToLeftWrapsFromRightEdge()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <text id="label" x="100" y="25" font-family="sans-serif" font-size="10" inline-size="15" direction="rtl" unicode-bidi="embed">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(20f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(90f, firstStart.X, 3);
        Assert.Equal(90f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 10f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeRightToLeftMixedBidiWrapsSimpleRuns()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="120" viewBox="0 0 160 120">
              <text id="label" x="120" y="25" font-family="sans-serif" font-size="10" inline-size="35" direction="rtl" unicode-bidi="embed">A &#x05D0;&#x05D1; B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var hebrewStart = metrics.GetStartPositionOfChar(1);
        var lastStart = metrics.GetStartPositionOfChar(3);
        Assert.True(firstStart.X < 120f, $"Expected RTL mixed inline-size text to anchor from the right edge, but X was {firstStart.X}.");
        Assert.True(hebrewStart.Y > firstStart.Y + 8f, $"Expected mixed bidi inline-size text to wrap at simple run boundaries, but positions were {firstStart} and {hebrewStart}.");
        Assert.True(lastStart.Y > hebrewStart.Y + 8f, $"Expected trailing LTR run to continue wrapped layout, but positions were {hebrewStart} and {lastStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeStyleDirectionRightToLeftWrapsFromRight()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="120" viewBox="0 0 160 120">
              <g style="direction: rtl; unicode-bidi: embed">
                <text id="label" x="120" y="25" font-family="sans-serif" font-size="10" inline-size="25">A B</text>
              </g>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.True(firstStart.X < 120f, $"Expected inherited style direction to anchor from the right edge, but X was {firstStart.X}.");
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected inherited style direction to wrap from the right edge, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeRightToLeftMixedBidiSingleLineUsesVisualPositions()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <text id="label" x="40" y="25" font-family="sans-serif" font-size="10" inline-size="100" direction="rtl" unicode-bidi="embed">A &#x05D0;&#x05D1;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);

        var latinStart = metrics.GetStartPositionOfChar(0);
        var firstHebrewStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(latinStart.Y, firstHebrewStart.Y, 3);
        Assert.True(latinStart.X > firstHebrewStart.X + 5f, $"Expected mixed RTL DOM metrics to report visual text positions, but A was at {latinStart} and the first Hebrew character was at {firstHebrewStart}.");
        Assert.InRange(metrics.GetSubStringLength(0, metrics.NumberOfChars), 35f, 45f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeMixedBidiSecondLineUsesGlobalCharacterIndexes()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="label" x="80" y="25" font-family="sans-serif" font-size="10" inline-size="100" white-space="pre-line" direction="rtl" unicode-bidi="embed">AA
            A &#x05D0;&#x05D1;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(6, metrics!.NumberOfChars);

        var firstLineStart = metrics.GetStartPositionOfChar(0);
        var secondLineLatinStart = metrics.GetStartPositionOfChar(2);
        var secondLineHebrewStart = metrics.GetStartPositionOfChar(4);
        Assert.True(secondLineLatinStart.Y > firstLineStart.Y + 8f, $"Expected mixed bidi content to stay indexed after the first forced line, but positions were {firstLineStart} and {secondLineLatinStart}.");
        Assert.Equal(secondLineLatinStart.Y, secondLineHebrewStart.Y, 3);
        Assert.True(secondLineLatinStart.X > secondLineHebrewStart.X + 5f, $"Expected second-line mixed RTL DOM metrics to use visual positions with global indexes, but A was at {secondLineLatinStart} and Hebrew was at {secondLineHebrewStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeMixedBidiForcedLinesReorderEachLine()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="label" x="80" y="25" font-family="sans-serif" font-size="10" inline-size="100" white-space="pre-line" direction="rtl" unicode-bidi="embed">A &#x05D0;&#x05D1;
            B &#x05D2;&#x05D3;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(8, metrics!.NumberOfChars);

        var firstLineLatinStart = metrics.GetStartPositionOfChar(0);
        var firstLineHebrewStart = metrics.GetStartPositionOfChar(2);
        var secondLineLatinStart = metrics.GetStartPositionOfChar(4);
        var secondLineHebrewStart = metrics.GetStartPositionOfChar(6);
        Assert.Equal(firstLineLatinStart.Y, firstLineHebrewStart.Y, 3);
        Assert.Equal(secondLineLatinStart.Y, secondLineHebrewStart.Y, 3);
        Assert.True(secondLineLatinStart.Y > firstLineLatinStart.Y + 8f, $"Expected forced line break to create a second bidi line, but positions were {firstLineLatinStart} and {secondLineLatinStart}.");
        Assert.True(firstLineLatinStart.X > firstLineHebrewStart.X + 5f, $"Expected first mixed RTL line to use visual order, but Latin was at {firstLineLatinStart} and Hebrew was at {firstLineHebrewStart}.");
        Assert.True(secondLineLatinStart.X > secondLineHebrewStart.X + 5f, $"Expected second mixed RTL line to use visual order, but Latin was at {secondLineLatinStart} and Hebrew was at {secondLineHebrewStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeCssDirectionAndUnicodeBidiInheritMixedVisualOrder()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="0 0 200 100">
              <g style="direction: rtl; unicode-bidi: embed">
                <text id="label" x="40" y="25" font-family="sans-serif" font-size="10" inline-size="100">A &#x05D0;&#x05D1;</text>
              </g>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);

        var latinStart = metrics.GetStartPositionOfChar(0);
        var firstHebrewStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(latinStart.Y, firstHebrewStart.Y, 3);
        Assert.True(latinStart.X > firstHebrewStart.X + 5f, $"Expected inherited CSS direction/unicode-bidi to use RTL visual ordering, but A was at {latinStart} and Hebrew was at {firstHebrewStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeCssDirectionOverridesPresentationAttribute()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="0 0 200 100">
              <text id="label" x="40" y="25" font-family="sans-serif" font-size="10" inline-size="100" direction="ltr" style="direction: rtl; unicode-bidi: embed">A &#x05D0;&#x05D1;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);

        var latinStart = metrics.GetStartPositionOfChar(0);
        var firstHebrewStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(latinStart.Y, firstHebrewStart.Y, 3);
        Assert.True(latinStart.X > firstHebrewStart.X + 5f, $"Expected CSS direction to outrank the presentation attribute, but A was at {latinStart} and Hebrew was at {firstHebrewStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeUnicodeBidiOverrideReordersDomPositions()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="100" viewBox="0 0 240 100">
              <text id="label" x="120" y="25" font-family="sans-serif" font-size="10" inline-size="100" direction="rtl" unicode-bidi="bidi-override">ABC</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var lastStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, lastStart.Y, 3);
        Assert.True(firstStart.X > lastStart.X + 15f, $"Expected RTL bidi-override to place the first logical character after the last logical character, but positions were {firstStart} and {lastStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeUnicodeBidiPlainTextUsesFirstStrongDirection()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="100" viewBox="0 0 220 100">
              <text id="label" x="40" y="25" font-family="sans-serif" font-size="10" inline-size="120" direction="ltr" unicode-bidi="plaintext">&#x05D0;&#x05D1; A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);

        var firstHebrewStart = metrics.GetStartPositionOfChar(0);
        var latinStart = metrics.GetStartPositionOfChar(3);
        Assert.Equal(firstHebrewStart.Y, latinStart.Y, 3);
        Assert.True(latinStart.X > firstHebrewStart.X + 15f, $"Expected unicode-bidi:plaintext to derive an RTL paragraph from the first strong character, but positions were {firstHebrewStart} and {latinStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeVerticalWrapsIntoColumns()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="140" viewBox="0 0 140 140">
              <text id="label" x="80" y="25" font-family="sans-serif" font-size="10" inline-size="15" writing-mode="tb">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(20f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.True(secondStart.X < firstStart.X - 10f, $"Expected wrapped vertical text to move into a leftward column, but X was {secondStart.X} vs {firstStart.X}.");
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeVerticalRightToLeftKeepsColumnWrapping()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="140" viewBox="0 0 140 140">
              <text id="label" x="80" y="25" font-family="sans-serif" font-size="10" inline-size="15" writing-mode="tb" direction="rtl" unicode-bidi="embed">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.True(secondStart.X < firstStart.X - 10f, $"Expected vertical direction=rtl inline-size layout to keep right-to-left column progression, but X was {secondStart.X} vs {firstStart.X}.");
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeVerticalRlDirectionRtlAdvancesBottomToTop()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="140" viewBox="0 0 140 140">
              <text id="label" x="80" y="80" font-family="sans-serif" font-size="10" inline-size="25" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed">&#x4E00;&#x4E8C; &#x4E09;&#x56DB;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);
        Assert.Equal(40f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        var thirdStart = metrics.GetStartPositionOfChar(2);
        var fourthStart = metrics.GetStartPositionOfChar(3);
        Assert.Equal(firstStart.X, secondStart.X, 3);
        Assert.True(secondStart.Y < firstStart.Y - 8f, $"Expected vertical-rl direction=rtl inline progression to move bottom-to-top, but Y was {secondStart.Y} vs {firstStart.Y}.");
        Assert.True(thirdStart.X < firstStart.X - 10f, $"Expected vertical-rl direction=rtl wrapping to advance into a right-to-left column, but X was {thirdStart.X} vs {firstStart.X}.");
        Assert.Equal(thirdStart.X, fourthStart.X, 3);
        Assert.True(fourthStart.Y < thirdStart.Y - 8f, $"Expected wrapped vertical-rl direction=rtl text to keep bottom-to-top progression, but Y was {fourthStart.Y} vs {thirdStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeVerticalLrWrapsIntoRightwardColumns()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="140" viewBox="0 0 140 140">
              <text id="label" x="40" y="25" font-family="sans-serif" font-size="10" inline-size="15" writing-mode="vertical-lr">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.True(secondStart.X > firstStart.X + 10f, $"Expected vertical-lr wrapped text to move into a rightward column, but X was {secondStart.X} vs {firstStart.X}.");
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeInsideWrapsWithDomPositions()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <defs>
                <rect id="shape" x="10" y="20" width="15" height="80" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(20f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 10f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeInsideRightToLeftAnchorsWrappedLinesToRightEdge()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <defs>
                <rect id="shape" x="10" y="20" width="15" height="80" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)" direction="rtl" unicode-bidi="embed">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(15f, firstStart.X, 3);
        Assert.Equal(15f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 10f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeSubtractUsesRemainingFragment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <rect id="shape" x="10" y="20" width="100" height="80" />
                <rect id="subtract" x="10" y="20" width="50" height="38" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)" shape-subtract="url(#subtract)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.True(metrics.FirstStartPosition.X >= 60f, $"Expected shape-subtract DOM metrics to use the remaining line fragment, but X was {metrics.FirstStartPosition.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeSubtractUsesLineBoxOverlap()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <rect id="shape" x="10" y="20" width="100" height="80" />
                <rect id="subtract" x="10" y="20" width="55" height="4" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)" shape-subtract="url(#subtract)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.True(metrics.FirstStartPosition.X >= 65f, $"Expected top-overlapping shape-subtract to exclude the first line fragment, but X was {metrics.FirstStartPosition.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_VerticalRtlShapeSubtractUsesLowerInlineFragment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="160" viewBox="0 0 160 160">
              <defs>
                <rect id="shape" x="40" y="20" width="80" height="100" />
                <rect id="subtract" x="40" y="60" width="80" height="20" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed" shape-inside="url(#shape)" shape-subtract="url(#subtract)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.True(metrics.FirstStartPosition.Y >= 110f, $"Expected vertical RTL shape layout to choose the lower open inline fragment, but Y was {metrics.FirstStartPosition.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_VerticalRlShapeInsideUsesRightEdgeColumnBand()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="140" viewBox="0 0 120 140">
              <defs>
                <rect id="shape" x="40" y="20" width="12" height="90" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed" shape-inside="url(#shape)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);

        var start = metrics.FirstStartPosition;
        Assert.True(float.IsFinite(start.X), $"Expected a finite vertical-rl shape X position, but X was {start.X}.");
        Assert.True(start.Y > 100f, $"Expected vertical-rl direction=rtl shape text to start at the bottom edge of the inline area, but Y was {start.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicInsetShapeInsideWrapsText()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="inset(20px 115px 20px 10px)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected inset shape-inside to wrap in the narrow basic shape, but Y values were {firstStart.Y} and {secondStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicInsetRoundNarrowsTopLine()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="inset(20px 30px 20px 10px round 40px / 40px)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.InRange(metrics.FirstStartPosition.X, 45f, 55f);
        Assert.True(metrics.FirstStartPosition.Y > 20f, $"Expected rounded inset baseline to remain inside the shape, but Y was {metrics.FirstStartPosition.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicShapeSubtractInsetRoundUsesCurvedExclusion()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="inset(20px 10px 20px 10px)" shape-subtract="inset(20px 70px 20px 20px round 20px)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.InRange(metrics.FirstStartPosition.X, 10f, 55f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicShapeInsideListFlowsOverflowIntoNextShape()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="inset(20px 155px 80px 10px) inset(20px 20px 80px 80px)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(80f, secondStart.X, 3);
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicShapeSubtractListUsesOpenFragment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="inset(20px 10px 20px 10px)" shape-subtract="inset(20px 120px 70px 10px) inset(20px 10px 70px 90px)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.InRange(metrics.FirstStartPosition.X, 60f, 90f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicCircleFarthestSideUsesResolvedCenter()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="0 0 200 100">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="circle(farthest-side at 25% 50%)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.True(metrics.FirstStartPosition.Y < -70f, $"Expected farthest-side radius to use the off-center distance to the far side, but Y was {metrics.FirstStartPosition.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicPolygonAcceptsFillRulePrefix()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="polygon(evenodd, 10px 20px, 25px 20px, 25px 100px, 10px 100px)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(10f, metrics.FirstStartPosition.X, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_EvenOddShapeInsideExcludesNestedSubpathHole()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <path id="shape" fill-rule="evenodd" d="M10 20 H110 V100 H10 Z M40 20 H80 V100 H40 Z" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(20f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
        Assert.True(secondStart.X >= 80f, $"Expected even-odd nested subpath hole to create a second same-line fragment, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_NonZeroShapeInsideKeepsSameDirectionNestedSubpathFilled()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <path id="shape" fill-rule="nonzero" d="M10 20 H110 V100 H10 Z M40 20 H80 V100 H40 Z" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(20f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
        Assert.True(secondStart.X > firstStart.X + 15f, $"Expected nonzero same-direction nested subpath to remain filled, but X values were {firstStart.X} and {secondStart.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_ViewBoxShapeBoxCreatesViewportShape()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="view-box">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.Equal(0f, metrics.FirstStartPosition.X, 3);
        Assert.True(metrics.FirstStartPosition.Y > 0f, $"Expected view-box shape to set a viewport-relative baseline, but Y was {metrics.FirstStartPosition.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicShapeUsesTrailingViewBoxShapeBox()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="inset(20px 105px 20px 10px) view-box">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected view-box-qualified inset to wrap in the basic shape, but Y values were {firstStart.Y} and {secondStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicShapeUsesTrailingFillBoxShapeBox()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="inset(20px 105px 20px 10px) fill-box">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected fill-box-qualified inset to wrap in the basic shape, but Y values were {firstStart.Y} and {secondStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_BasicShapeUsesLeadingFillBoxShapeBox()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="fill-box inset(20px 105px 20px 10px)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected leading fill-box inset to wrap in the basic shape, but Y values were {firstStart.Y} and {secondStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeBoxKeywordCreatesViewportShape()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="30" y="45" font-family="sans-serif" font-size="10" shape-inside="border-box">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.Equal(30f, metrics.FirstStartPosition.X, 3);
        Assert.True(metrics.FirstStartPosition.Y > 0f, $"Expected shape-box keyword to create a text-geometry-backed static text area, but Y was {metrics.FirstStartPosition.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_FillBoxShapeBoxUsesTextGeometryBox()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="100" viewBox="0 0 160 100">
              <text id="label" x="30" y="50" font-family="sans-serif" font-size="10" shape-inside="fill-box">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.True(metrics.FirstStartPosition.X >= 29f, $"Expected fill-box shape to use the text geometry box instead of the viewport, but X was {metrics.FirstStartPosition.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_StrokeBoxShapeBoxInflatesTextGeometryBox()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="fill" x="40" y="60" font-family="sans-serif" font-size="10" shape-inside="fill-box">A</text>
              <text id="stroke" x="40" y="60" font-family="sans-serif" font-size="10" stroke="#000" stroke-width="20" shape-inside="stroke-box">A</text>
            </svg>
            """);
        var fillText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "fill");
        var strokeText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "stroke");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var fillSucceeded = InvokeTryCreateTextContentMetrics(fillText, viewport, assetLoader, out var fillMetrics);
        var strokeSucceeded = InvokeTryCreateTextContentMetrics(strokeText, viewport, assetLoader, out var strokeMetrics);

        Assert.True(fillSucceeded);
        Assert.True(strokeSucceeded);
        Assert.NotNull(fillMetrics);
        Assert.NotNull(strokeMetrics);
        Assert.True(strokeMetrics!.FirstStartPosition.X < fillMetrics!.FirstStartPosition.X, $"Expected stroke-box to expand left of fill-box, but positions were {strokeMetrics.FirstStartPosition.X} and {fillMetrics.FirstStartPosition.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_LayoutShapeBoxesMapToSvgFillAndStrokeBoxes()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="content" x="40" y="60" font-family="sans-serif" font-size="10" shape-inside="content-box">A</text>
              <text id="padding" x="40" y="60" font-family="sans-serif" font-size="10" shape-inside="padding-box">A</text>
              <text id="border" x="40" y="60" font-family="sans-serif" font-size="10" stroke="#000" stroke-width="20" shape-inside="border-box">A</text>
              <text id="margin" x="40" y="60" font-family="sans-serif" font-size="10" stroke="#000" stroke-width="20" shape-inside="margin-box">A</text>
            </svg>
            """);
        var contentText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "content");
        var paddingText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "padding");
        var borderText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "border");
        var marginText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "margin");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var contentSucceeded = InvokeTryCreateTextContentMetrics(contentText, viewport, assetLoader, out var contentMetrics);
        var paddingSucceeded = InvokeTryCreateTextContentMetrics(paddingText, viewport, assetLoader, out var paddingMetrics);
        var borderSucceeded = InvokeTryCreateTextContentMetrics(borderText, viewport, assetLoader, out var borderMetrics);
        var marginSucceeded = InvokeTryCreateTextContentMetrics(marginText, viewport, assetLoader, out var marginMetrics);

        Assert.True(contentSucceeded);
        Assert.True(paddingSucceeded);
        Assert.True(borderSucceeded);
        Assert.True(marginSucceeded);
        Assert.NotNull(contentMetrics);
        Assert.NotNull(paddingMetrics);
        Assert.NotNull(borderMetrics);
        Assert.NotNull(marginMetrics);
        Assert.Equal(contentMetrics!.FirstStartPosition.X, paddingMetrics!.FirstStartPosition.X, 3);
        Assert.Equal(borderMetrics!.FirstStartPosition.X, marginMetrics!.FirstStartPosition.X, 3);
        Assert.True(borderMetrics.FirstStartPosition.X < contentMetrics.FirstStartPosition.X, $"Expected border-box/margin-box to use the SVG stroke box and expand left of content-box/padding-box, but positions were {borderMetrics.FirstStartPosition.X} and {contentMetrics.FirstStartPosition.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_ImageShapeThresholdUsesAlphaIntervals()
    {
        const string alphaPng = "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAE0lEQVR4nGNgYGD4D8UNUEyqAACVLwv51oy5YgAAAABJRU5ErkJggg==";
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">
              <text id="wide" font-family="sans-serif" font-size="10" shape-inside="url(data:image/png;base64,{alphaPng}) view-box" shape-image-threshold="0">A B</text>
              <text id="threshold" font-family="sans-serif" font-size="10" shape-inside="url(data:image/png;base64,{alphaPng}) view-box" shape-image-threshold="0.75">A B</text>
            </svg>
            """);
        var wideText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "wide");
        var thresholdText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "threshold");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(15f);

        var wideSucceeded = InvokeTryCreateTextContentMetrics(wideText, viewport, assetLoader, out var wideMetrics);
        var thresholdSucceeded = InvokeTryCreateTextContentMetrics(thresholdText, viewport, assetLoader, out var thresholdMetrics);

        Assert.True(wideSucceeded);
        Assert.True(thresholdSucceeded);
        Assert.NotNull(wideMetrics);
        Assert.NotNull(thresholdMetrics);

        var wideFirst = wideMetrics!.GetStartPositionOfChar(0);
        var wideSecond = wideMetrics.GetStartPositionOfChar(1);
        var thresholdFirst = thresholdMetrics!.GetStartPositionOfChar(0);
        var thresholdSecond = thresholdMetrics.GetStartPositionOfChar(1);
        Assert.Equal(wideFirst.Y, wideSecond.Y, 3);
        Assert.True(thresholdSecond.Y > thresholdFirst.Y + 8f, $"Expected shape-image-threshold to exclude the half-alpha columns and force wrapping, but Y values were {thresholdFirst.Y} and {thresholdSecond.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_ImageShapeUsesSkiaDecodedJpeg()
    {
        var jpegUri = CreateOpaqueEncodedImageDataUri(SkiaSharp.SKEncodedImageFormat.Jpeg, "image/jpeg");
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url({jpegUri}) view-box">AB</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
        Assert.True(secondStart.X > firstStart.X + 8f, $"Expected JPEG image shape decoding to expose an opaque same-line shape, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_ImageShapeThresholdExcludesEqualAlpha()
    {
        var alphaPngUri = CreateSplitAlphaPngDataUri(128, 255);
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url({alphaPngUri}) view-box" shape-image-threshold="0.5019608">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.True(metrics!.FirstStartPosition.X >= 40f, $"Expected pixels whose alpha equals the threshold to be excluded from the image shape, but X was {metrics.FirstStartPosition.X}.");
    }

    [Fact]
    public void SvgCssShapeImageSampler_IndexedTransparencyCreatesAlphaIntervals()
    {
        var png = CreateIndexedTransparencyPng();

        var succeeded = InvokeTryCreateCssShapeAlphaPath(png, SKRect.Create(0f, 0f, 20f, 10f), 0f, out var path);

        Assert.True(succeeded);
        Assert.NotNull(path);
        Assert.Equal(10f, path!.Bounds.Left, 3);
        Assert.Equal(0f, path.Bounds.Top, 3);
        Assert.Equal(20f, path.Bounds.Right, 3);
        Assert.Equal(10f, path.Bounds.Bottom, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeInsideDefinesAreaWhenInlineSizeAlsoPresent()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <rect id="shape" x="10" y="20" width="80" height="80" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" inline-size="15" shape-inside="url(#shape)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
        Assert.True(secondStart.X > firstStart.X + 15f, $"Expected shape-inside to define the content width instead of inline-size, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeSubtractAloneDoesNotDisableInlineSize()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" shape-subtract="inset(0px)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected inline-size wrapping to remain active when shape-subtract has no shape-inside, but Y values were {firstStart.Y} and {secondStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeShapeSubtractExcludesLineFragment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <defs>
                <rect id="subtract" x="10" y="20" width="48" height="24" />
              </defs>
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="90" shape-subtract="url(#subtract)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        Assert.True(firstStart.X >= 58f, $"Expected inline-size shape-subtract to exclude the first line fragment, but first start was {firstStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_RtlInlineSizeShapeSubtractExcludesLineFragment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <defs>
                <rect id="subtract" x="10" y="20" width="48" height="24" />
              </defs>
              <text id="label" x="100" y="25" font-family="sans-serif" font-size="10" inline-size="90" direction="rtl" unicode-bidi="embed" shape-subtract="url(#subtract)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var lastStart = metrics.GetStartPositionOfChar(2);
        Assert.True(firstStart.X >= 58f, $"Expected RTL inline-size shape-subtract to exclude the first line fragment, but first start was {firstStart}.");
        Assert.True(lastStart.X >= 58f, $"Expected RTL inline-size shape-subtract to keep text in the remaining fragment, but last start was {lastStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_VerticalRtlInlineSizeShapeSubtractUsesLowerInlineFragment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="160" viewBox="0 0 160 160">
              <defs>
                <rect id="subtract" x="80" y="20" width="24" height="90" />
              </defs>
              <text id="label" x="90" y="140" font-family="sans-serif" font-size="10" inline-size="120" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed" shape-subtract="url(#subtract)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.True(metrics.FirstStartPosition.Y >= 110f, $"Expected vertical RTL inline-size shape-subtract to choose the lower open inline fragment, but Y was {metrics.FirstStartPosition.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapePaddingDoesNotInheritFromAncestor()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <defs>
                <rect id="shape" x="10" y="20" width="40" height="80" />
              </defs>
              <g style="shape-padding: 10px">
                <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)">A</text>
              </g>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.Equal(10f, metrics.FirstStartPosition.X, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_StyledShapePaddingInsetsShapeInside()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <defs>
                <rect id="shape" x="10" y="20" width="40" height="80" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" style="shape-inside: url(#shape); shape-padding: 5px">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.Equal(15f, metrics.FirstStartPosition.X, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_StyledShapeMarginExpandsExclusion()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <rect id="shape" x="10" y="20" width="100" height="80" />
                <rect id="subtract" x="10" y="20" width="50" height="38" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" style="shape-inside: url(#shape); shape-subtract: url(#subtract); shape-margin: 5px">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.True(metrics.FirstStartPosition.X >= 65f, $"Expected shape-margin to expand the exclusion, but X was {metrics.FirstStartPosition.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizePreLineForcesLineBreak()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"80\" viewBox=\"0 0 120 80\">" +
            "<text id=\"label\" x=\"10\" y=\"25\" font-family=\"sans-serif\" font-size=\"10\" inline-size=\"100\" white-space=\"pre-line\">A\nB</text>" +
            "</svg>");
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);
        Assert.Equal(20f, metrics.ComputedTextLength, 3);
        Assert.Equal(20f, metrics.FullSubstringLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 10f);

        var secondExtent = metrics.GetExtentOfChar(1);
        var secondHitPoint = new SKPoint(
            (secondExtent.Left + secondExtent.Right) * 0.5f,
            (secondExtent.Top + secondExtent.Bottom) * 0.5f);
        Assert.Equal(1, metrics.GetCharNumAtPosition(secondHitPoint));
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeDoesNotBreakAtNoBreakSpace()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">A&#160;B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var lastStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, lastStart.Y, 3);
        Assert.True(lastStart.X > firstStart.X + 15f, $"Expected no-break space to keep the word on one line, but positions were {firstStart} and {lastStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeDoesNotBreakAtFigureSpace()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">A&#8199;B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var lastStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, lastStart.Y, 3);
        Assert.True(lastStart.X > firstStart.X + 15f, $"Expected figure space to keep the word on one line, but positions were {firstStart} and {lastStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeWrapsCjkWithoutSpaces()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">&#x4E00;&#x4E8C;&#x4E09;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        var thirdStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.Equal(10f, thirdStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected CJK break opportunity between ideographs, but Y values were {firstStart.Y} and {secondStart.Y}.");
        Assert.True(thirdStart.Y > secondStart.Y + 8f, $"Expected CJK break opportunity between ideographs, but Y values were {secondStart.Y} and {thirdStart.Y}.");
    }

    [Theory]
    [InlineData("A\u200BB")]
    [InlineData("A\u00ADB")]
    public void TryCreateTextContentMetrics_InlineSizeUsesInvisibleBreakOpportunity(string text)
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">{text}</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected invisible break opportunity to wrap the following character, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeBreaksAfterDash()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="20">A-B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var dashStart = metrics.GetStartPositionOfChar(1);
        var secondStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, dashStart.Y, 3);
        Assert.Equal(20f, dashStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected dash to create a break opportunity, but positions were {firstStart}, {dashStart}, and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeBreaksAfterSlashSeparator()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="20">A/B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var slashStart = metrics.GetStartPositionOfChar(1);
        var secondStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, slashStart.Y, 3);
        Assert.Equal(20f, slashStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected slash to create a separator break opportunity, but positions were {firstStart}, {slashStart}, and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeKeepsDecimalSeparatorWithDigits()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="20">1.2</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var lastStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, lastStart.Y, 3);
        Assert.True(lastStart.X > firstStart.X + 15f, $"Expected decimal separator to stay in the numeric run, but positions were {firstStart} and {lastStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeDoesNotLeaveWordInitialHyphenAlone()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="10">-A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var hyphenStart = metrics.GetStartPositionOfChar(0);
        var letterStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(hyphenStart.Y, letterStart.Y, 3);
        Assert.True(letterStart.X > hyphenStart.X + 5f, $"Expected a word-initial hyphen to remain with the following letter, but positions were {hyphenStart} and {letterStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeKeepsClosingParenthesisWithFollowingLetter()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="20">A)B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var lastStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, lastStart.Y, 3);
        Assert.True(lastStart.X > firstStart.X + 15f, $"Expected closing parenthesis followed by a letter to stay in the same run, but positions were {firstStart} and {lastStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeLineHeightControlsWrappedLineAdvance()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" line-height="3">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.InRange(secondStart.Y - firstStart.Y, 29f, 31f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeLineHeightPercentageUsesFontSize()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" line-height="250%">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(25f, secondStart.Y - firstStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeOverflowWrapAnywhereBreaksLongWord()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" overflow-wrap="anywhere">ABC</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected overflow-wrap:anywhere to wrap inside the word, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeWordBreakBreakAllBreaksLongWord()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" word-break="break-all">ABC</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected word-break:break-all to wrap inside the word, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeWordBreakKeepAllSuppressesCjkBreaks()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" word-break="keep-all">&#x4E00;&#x4E8C;&#x4E09;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var thirdStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, thirdStart.Y, 3);
        Assert.True(thirdStart.X > firstStart.X + 15f, $"Expected word-break:keep-all to suppress CJK inline-size breaks, but positions were {firstStart} and {thirdStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeDoesNotBreakComplexContextScriptWithoutDictionaryProvider()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">&#x0E01;&#x0E02;&#x0E04;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
        Assert.True(secondStart.X > firstStart.X + 8f, $"Expected dictionaryless complex-context script text to stay unbroken, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeEmergencyBreaksComplexContextScript()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" overflow-wrap="anywhere">&#x0E01;&#x0E02;&#x0E04;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected overflow-wrap:anywhere to provide emergency complex-context breaks, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeLineBreakAnywhereIgnoresPunctuationSuppression()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="10" line-break="anywhere">A!</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected line-break:anywhere to break before punctuation when needed, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeBreaksAfterClosingPunctuation()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="20">A,B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var commaStart = metrics.GetStartPositionOfChar(1);
        var secondStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, commaStart.Y, 3);
        Assert.Equal(20f, commaStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected a break opportunity after comma punctuation, but positions were {firstStart}, {commaStart}, and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeLineBreakStrictKeepsSmallKanaWithPrevious()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="20" line-break="strict">&#x3042;&#x3041;&#x3044;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var smallKanaStart = metrics.GetStartPositionOfChar(1);
        var thirdStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, smallKanaStart.Y, 3);
        Assert.Equal(20f, smallKanaStart.X, 3);
        Assert.Equal(10f, thirdStart.X, 3);
        Assert.True(thirdStart.Y > firstStart.Y + 8f, $"Expected line-break:strict to keep the small kana with the previous character, but positions were {firstStart}, {smallKanaStart}, and {thirdStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizePreservesExplicitBidiEmbeddingControls()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">A&#x202B; B&#x202C; C</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.True(metrics!.NumberOfChars > 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var embeddedLetterStart = metrics.GetStartPositionOfChar(3);
        Assert.Equal(firstStart.Y, embeddedLetterStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizePreservesExplicitBidiIsolateControls()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">A&#x2067; B&#x2069; C</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.True(metrics!.NumberOfChars > 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var isolatedLetterStart = metrics.GetStartPositionOfChar(3);
        Assert.Equal(firstStart.Y, isolatedLetterStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeDoesNotBreakAfterZeroWidthJoiner()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">A&#x200D; B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.True(metrics!.NumberOfChars > 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var joinedLetterStart = metrics.GetStartPositionOfChar(3);
        Assert.Equal(firstStart.Y, joinedLetterStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeOverflowWrapAnywhereKeepsCombiningClusterTogether()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" overflow-wrap="anywhere">A&#x0301;B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var combiningStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(firstStart.Y, combiningStart.Y, 3);
        Assert.Equal(firstStart.X, combiningStart.X, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeNoWrapSuppressesSoftWraps()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" white-space="nowrap">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var lastStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstStart.Y, lastStart.Y, 3);
        Assert.True(lastStart.X > firstStart.X + 15f, $"Expected white-space:nowrap to suppress inline-size soft wrapping, but positions were {firstStart} and {lastStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizePreWrapPreservesLeadingSpace()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="100" white-space="pre-wrap"> A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var spaceStart = metrics.GetStartPositionOfChar(0);
        var letterStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, spaceStart.X, 3);
        Assert.Equal(20f, letterStart.X, 3);
        Assert.Equal(spaceStart.Y, letterStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeBreakSpacesWrapsPreservedSpaces()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="20" white-space="break-spaces">A  B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var lastStart = metrics.GetStartPositionOfChar(3);
        Assert.True(lastStart.Y > firstStart.Y + 8f, $"Expected break-spaces to preserve spaces while allowing a later line break, but Y values were {firstStart.Y} and {lastStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeDescendantBreakSpacesWrapsPreservedSpaces()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="20"><tspan white-space="break-spaces">A  B</tspan></text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var lastStart = metrics.GetStartPositionOfChar(3);
        Assert.True(lastStart.Y > firstStart.Y + 8f, $"Expected descendant break-spaces to preserve spaces while allowing a later line break, but Y values were {firstStart.Y} and {lastStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizePreLinePreservesEmptyForcedLine()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"100\" viewBox=\"0 0 120 100\">" +
            "<text id=\"label\" x=\"10\" y=\"25\" font-family=\"sans-serif\" font-size=\"10\" inline-size=\"100\" white-space=\"pre-line\">A\n\nB</text>" +
            "</svg>");
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 15f, $"Expected the empty pre-line row to advance layout, but Y was {secondStart.Y} vs {firstStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeDescendantPreLinePreservesBreak()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"80\" viewBox=\"0 0 120 80\">" +
            "<text id=\"label\" x=\"10\" y=\"25\" font-family=\"sans-serif\" font-size=\"10\" inline-size=\"100\"><tspan white-space=\"pre-line\">A\nB</tspan></text>" +
            "</svg>");
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 10f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeOverflowMarkerKeepsDomCharacters()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" text-overflow="ellipsis">ABC</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);
        Assert.Equal(30f, metrics.ComputedTextLength, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizePositionedDescendantFallsBackToPositionedMetrics()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15">A<tspan x="50" y="60">B</tspan>C</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var positionedStart = metrics.GetStartPositionOfChar(1);
        var tailStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(new SKPoint(10f, 25f), firstStart);
        Assert.Equal(new SKPoint(50f, 60f), positionedStart);
        Assert.True(tailStart.X > positionedStart.X + 5f, $"Expected text after positioned descendant to continue from the positioned chunk, but X was {tailStart.X} vs {positionedStart.X}.");
        Assert.Equal(positionedStart.Y, tailStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthRelativePositionedDescendantUsesFlattenedMetrics()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" textLength="90">A<tspan dx="5">B</tspan>C</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);
        Assert.Equal(90f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var positionedStart = metrics.GetStartPositionOfChar(1);
        var tailStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(new SKPoint(10f, 25f), firstStart);
        Assert.Equal(55f, positionedStart.X, 3);
        Assert.Equal(firstStart.Y, positionedStart.Y, 3);
        Assert.Equal(95f, tailStart.X, 3);
        Assert.Equal(firstStart.Y, tailStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthWrapsBeforeSpacingAdjustment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="25" textLength="80">AB CD</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);
        Assert.Equal(80f, metrics.ComputedTextLength, 3);

        var a = metrics.GetStartPositionOfChar(0);
        var b = metrics.GetStartPositionOfChar(1);
        var c = metrics.GetStartPositionOfChar(2);
        var d = metrics.GetStartPositionOfChar(3);
        Assert.Equal(10f, a.X, 3);
        Assert.Equal(40f, b.X, 3);
        Assert.Equal(a.Y, b.Y, 3);
        Assert.Equal(10f, c.X, 3);
        Assert.Equal(40f, d.X, 3);
        Assert.Equal(c.Y, d.Y, 3);
        Assert.True(c.Y > a.Y + 8f, $"Expected textLength to be applied after inline-size wrapping, but second line Y was {c.Y} and first line Y was {a.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthWrapsOnShapedClusterBoundaries()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="12" textLength="24" overflow-wrap="anywhere">fiX</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new ShapedClusterAdvanceAssetLoader();

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstClusterStart = metrics.GetStartPositionOfChar(0);
        var firstClusterContinuation = metrics.GetStartPositionOfChar(1);
        var secondClusterStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstClusterStart.Y, firstClusterContinuation.Y, 3);
        Assert.True(secondClusterStart.Y > firstClusterStart.Y + 8f, $"Expected line fitting to keep the shaped fi cluster together before wrapping X, but positions were {firstClusterStart}, {firstClusterContinuation}, {secondClusterStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthDistributesSpacingBetweenShapedClusters()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="80" viewBox="0 0 160 80">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="100" textLength="40">fiX</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new ShapedClusterAdvanceAssetLoader();

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);

        var firstClusterStart = metrics.GetStartPositionOfChar(0);
        var firstClusterContinuation = metrics.GetStartPositionOfChar(1);
        var secondClusterStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(firstClusterStart.Y, secondClusterStart.Y, 3);
        Assert.Equal(firstClusterStart.X, firstClusterContinuation.X, 3);
        Assert.True(secondClusterStart.X > firstClusterStart.X + 25f, $"Expected textLength spacing to be inserted after the shaped fi cluster, but positions were {firstClusterStart}, {firstClusterContinuation}, {secondClusterStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthSpacingAndGlyphsWrapsBeforeScaling()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="25" textLength="80" lengthAdjust="spacingAndGlyphs">AB CD</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);
        Assert.Equal(80f, metrics.ComputedTextLength, 3);

        var a = metrics.GetStartPositionOfChar(0);
        var b = metrics.GetStartPositionOfChar(1);
        var c = metrics.GetStartPositionOfChar(2);
        var d = metrics.GetStartPositionOfChar(3);
        Assert.Equal(10f, a.X, 3);
        Assert.True(b.X > a.X + 15f, $"Expected spacingAndGlyphs to scale the first wrapped line, but A was at {a.X} and B was at {b.X}.");
        Assert.Equal(a.Y, b.Y, 3);
        Assert.Equal(10f, c.X, 3);
        Assert.True(d.X > c.X + 15f, $"Expected spacingAndGlyphs to scale the second wrapped line, but C was at {c.X} and D was at {d.X}.");
        Assert.Equal(c.Y, d.Y, 3);
        Assert.True(c.Y > a.Y + 8f, $"Expected spacingAndGlyphs layout to wrap before scaling, but C was at {c} and A was at {a}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthWrapsRelativePositionedDescendant()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="25" textLength="80">AB <tspan dx="5">CD</tspan></text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);
        Assert.Equal(80f, metrics.ComputedTextLength, 3);

        var a = metrics.GetStartPositionOfChar(0);
        var b = metrics.GetStartPositionOfChar(1);
        var c = metrics.GetStartPositionOfChar(2);
        var d = metrics.GetStartPositionOfChar(3);
        Assert.Equal(10f, a.X, 3);
        Assert.Equal(40f, b.X, 3);
        Assert.Equal(a.Y, b.Y, 3);
        Assert.Equal(15f, c.X, 3);
        Assert.Equal(45f, d.X, 3);
        Assert.Equal(c.Y, d.Y, 3);
        Assert.True(c.Y > a.Y + 8f, $"Expected dx-positioned descendant to remain on the wrapped second line, but C was at {c} and A was at {a}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthPreLinePreservesExplicitLineBreaks()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"140\" height=\"100\" viewBox=\"0 0 140 100\">" +
            "<text id=\"label\" x=\"10\" y=\"25\" font-family=\"sans-serif\" font-size=\"10\" inline-size=\"100\" textLength=\"80\" white-space=\"pre-line\">A\nB</text>" +
            "</svg>");
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(10f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected textLength inline-size pre-line content to keep the forced line break, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthPreLineWrapsBeforeSpacingAdjustment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"140\" height=\"100\" viewBox=\"0 0 140 100\">" +
            "<text id=\"label\" x=\"10\" y=\"25\" font-family=\"sans-serif\" font-size=\"10\" inline-size=\"100\" textLength=\"80\" white-space=\"pre-line\">AB\nCD</text>" +
            "</svg>");
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);
        Assert.Equal(80f, metrics.ComputedTextLength, 3);

        var a = metrics.GetStartPositionOfChar(0);
        var b = metrics.GetStartPositionOfChar(1);
        var c = metrics.GetStartPositionOfChar(2);
        var d = metrics.GetStartPositionOfChar(3);
        Assert.Equal(10f, a.X, 3);
        Assert.Equal(40f, b.X, 3);
        Assert.Equal(a.Y, b.Y, 3);
        Assert.Equal(10f, c.X, 3);
        Assert.Equal(40f, d.X, 3);
        Assert.Equal(c.Y, d.Y, 3);
        Assert.True(c.Y > a.Y + 8f, $"Expected explicit pre-line break to remain a wrapped textLength line break, but C was at {c} and A was at {a}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthUsesNaturalLineProportions()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"160\" height=\"110\" viewBox=\"0 0 160 110\">" +
            "<text id=\"label\" x=\"10\" y=\"25\" font-family=\"sans-serif\" font-size=\"10\" inline-size=\"100\" textLength=\"80\" white-space=\"pre-line\">A\nBCD</text>" +
            "</svg>");
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.NumberOfChars);
        Assert.Equal(80f, metrics.ComputedTextLength, 3);

        var a = metrics.GetStartPositionOfChar(0);
        var b = metrics.GetStartPositionOfChar(1);
        var c = metrics.GetStartPositionOfChar(2);
        var d = metrics.GetStartPositionOfChar(3);
        Assert.Equal(10f, a.X, 3);
        Assert.Equal(10f, b.X, 3);
        Assert.True(c.X > b.X + 20f, $"Expected the longer second line to receive more than an equal line split, but B was {b} and C was {c}.");
        Assert.True(d.X > c.X + 20f, $"Expected proportional root textLength spacing to continue across the longer second line, but C was {c} and D was {d}.");
        Assert.True(b.Y > a.Y + 8f, $"Expected the explicit line break before B to be preserved, but A was {a} and B was {b}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthAbsolutePositionedDescendantUsesFlattenedMetrics()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="15" textLength="90">A<tspan x="50" y="60">B</tspan>C</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);
        Assert.Equal(90f, metrics.ComputedTextLength, 3);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var positionedStart = metrics.GetStartPositionOfChar(1);
        var tailStart = metrics.GetStartPositionOfChar(2);
        Assert.Equal(new SKPoint(10f, 25f), firstStart);
        Assert.Equal(new SKPoint(50f, 60f), positionedStart);
        Assert.True(tailStart.X > positionedStart.X + 5f, $"Expected absolute-position textLength layout to keep text after the positioned chunk, but X was {tailStart.X} vs {positionedStart.X}.");
        Assert.Equal(positionedStart.Y, tailStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeAbsoluteAndRelativeDescendantsUseSharedMetrics()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="150" height="100" viewBox="0 0 150 100">
              <text id="label" x="10" y="20" font-family="sans-serif" font-size="10" inline-size="35">A<tspan x="40" y="55" dx="3" dy="4">B</tspan><tspan dx="5" dy="2">C</tspan></text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);
        Assert.Equal(new SKPoint(10f, 20f), metrics.GetStartPositionOfChar(0));
        Assert.Equal(new SKPoint(43f, 59f), metrics.GetStartPositionOfChar(1));
        Assert.Equal(new SKPoint(58f, 61f), metrics.GetStartPositionOfChar(2));
    }

    [Fact]
    public void TryCreateSharedInlineSizeTextLayoutResult_NestedTextLengthSpansKeepOwningElement()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="120">A<tspan id="length-owner" textLength="50">BC</tspan>D</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateSharedInlineSizeTextLayoutResult(svgText, 10f, 25f, viewport, assetLoader, out var layout);

        Assert.True(succeeded);
        Assert.NotNull(layout);
        var line = Assert.Single(layout!.Lines);
        var ownedSpan = Assert.Single(line.PositionedSpans, static span => span.Text == "BC");
        Assert.NotNull(ownedSpan.TextLengthSource);
        Assert.Equal("length-owner", ownedSpan.TextLengthSource!.ID);
        Assert.Equal(20f, ownedSpan.NaturalAdvance, 3);
        Assert.Equal(50f, ownedSpan.AppliedTextLength, 3);
        Assert.Equal(10f, line.PositionedSpans[0].Placements[0].Point.X, 3);
        Assert.True(ownedSpan.Placements[1].Point.X > ownedSpan.Placements[0].Point.X + 30f, $"Expected nested textLength ownership to spread B/C placements, but positions were {ownedSpan.Placements[0].Point} and {ownedSpan.Placements[1].Point}.");
    }

    [Fact]
    public void TryCreateSharedInlineSizeTextLayoutResult_ShapeInsideTextLengthAllowsDescendantShapeStyle()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="120" viewBox="0 0 160 120">
              <defs>
                <rect id="shape" x="10" y="20" width="22" height="60" />
              </defs>
              <text id="label"
                    font-family="sans-serif"
                    font-size="10"
                    shape-inside="url(#shape)"
                    textLength="40"
                    lengthAdjust="spacing"
                    line-break="anywhere">A<tspan shape-inside="inset(0)">B</tspan>C</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateSharedInlineSizeTextLayoutResult(svgText, 10f, 25f, viewport, assetLoader, out var layout);

        Assert.True(succeeded);
        Assert.NotNull(layout);
        Assert.Equal(3, layout!.DomNumberOfChars);
        Assert.True(layout.Lines.Count >= 2, $"Expected shape-inside to wrap the textLength layout, but got {layout.Lines.Count} line(s).");
    }

    [Fact]
    public void TryCreateSharedInlineSizeTextLayoutResult_VerticalRlRtlTextLengthReportsLineProgression()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="140" viewBox="0 0 160 140">
              <text id="label" x="90" y="25" font-family="sans-serif" font-size="10" inline-size="25" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed" textLength="80">AB CD</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateSharedInlineSizeTextLayoutResult(svgText, 90f, 25f, viewport, assetLoader, out var layout);

        Assert.True(succeeded);
        Assert.NotNull(layout);
        Assert.Equal(80f, layout!.ComputedTextLength, 3);
        Assert.True(layout.Lines.Count >= 2, $"Expected vertical textLength content to wrap into multiple columns, but got {layout.Lines.Count}.");
        Assert.All(layout.Lines, static line =>
        {
            Assert.Equal("VerticalRightToLeftColumns", line.Flow);
            Assert.Equal(-1, line.InlineProgression);
        });
        Assert.True(layout.Lines[1].BaselineOrigin.X < layout.Lines[0].BaselineOrigin.X - 8f, $"Expected vertical-rl columns to progress right-to-left, but X values were {layout.Lines[0].BaselineOrigin.X} and {layout.Lines[1].BaselineOrigin.X}.");
        Assert.True(layout.Lines[0].PositionedSpans[0].Placements[1].Point.Y < layout.Lines[0].PositionedSpans[0].Placements[0].Point.Y, "Expected direction=rtl vertical inline progression to move bottom-to-top.");
    }

    [Fact]
    public void TryCreateSharedInlineSizeTextLayoutResult_ReordersBidiVisuallyPerWrappedLine()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="label"
                    x="20"
                    y="25"
                    font-family="sans-serif"
                    font-size="10"
                    inline-size="30"
                    textLength="60"
                    lengthAdjust="spacing"
                    line-break="anywhere"
                    direction="rtl"
                    unicode-bidi="embed">A&#x05D0;&#x05D1;B&#x05D2;&#x05D3;</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var sharedSucceeded = InvokeTryCreateSharedInlineSizeTextLayoutResult(svgText, 20f, 25f, viewport, assetLoader, out var layout);
        var metricsSucceeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(sharedSucceeded);
        Assert.True(metricsSucceeded);
        Assert.NotNull(layout);
        Assert.NotNull(metrics);
        Assert.True(layout!.Lines.Count >= 2, $"Expected mixed bidi text to wrap into at least two lines, but got {layout.Lines.Count}.");

        var firstLineLatin = metrics!.GetStartPositionOfChar(0);
        var firstLineHebrew = metrics.GetStartPositionOfChar(1);
        var secondLineLatin = metrics.GetStartPositionOfChar(3);
        var secondLineHebrew = metrics.GetStartPositionOfChar(4);

        Assert.Equal(firstLineLatin.Y, firstLineHebrew.Y, 3);
        Assert.Equal(secondLineLatin.Y, secondLineHebrew.Y, 3);
        Assert.True(secondLineLatin.Y > firstLineLatin.Y + 8f, $"Expected the second logical bidi line below the first, but positions were {firstLineLatin} and {secondLineLatin}.");
        Assert.True(firstLineLatin.X > firstLineHebrew.X + 5f, $"Expected first wrapped line to place the Latin run after the Hebrew run visually, but positions were {firstLineLatin} and {firstLineHebrew}.");
        Assert.True(secondLineLatin.X > secondLineHebrew.X + 5f, $"Expected second wrapped line to resolve bidi independently, but positions were {secondLineLatin} and {secondLineHebrew}.");
        Assert.Equal(0, layout.DomClusters[0].StartCharIndex);
        Assert.Equal(1, layout.DomClusters[1].StartCharIndex);
        Assert.Equal(3, layout.DomClusters[3].StartCharIndex);
    }

    [Fact]
    public void TryCreateSharedInlineSizeTextLayoutResult_DomMetricsMatchTextContentMetrics()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="35" textLength="90">A<tspan dx="5">B</tspan>C</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var sharedSucceeded = InvokeTryCreateSharedInlineSizeTextLayoutResult(svgText, 10f, 25f, viewport, assetLoader, out var layout);
        var metricsSucceeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(sharedSucceeded);
        Assert.True(metricsSucceeded);
        Assert.NotNull(layout);
        Assert.NotNull(metrics);
        Assert.Equal(metrics!.NumberOfChars, layout!.DomNumberOfChars);
        Assert.Equal(metrics.ComputedTextLength, layout.DomComputedTextLength, 3);
        Assert.Equal(metrics.GetStartPositionOfChar(0), layout.DomClusters[0].StartPoint);
        Assert.Equal(metrics.GetStartPositionOfChar(1), layout.DomClusters[1].StartPoint);
        Assert.Equal(metrics.GetStartPositionOfChar(2), layout.DomClusters[2].StartPoint);
        Assert.Equal(metrics.FullSubstringLength, layout.DomClusters[^1].EndOffset - layout.DomClusters[0].StartOffset, 3);
    }

    [Fact]
    public void TryCreateSharedInlineSizeTextLayoutResult_AppliesRootDxDyToLayoutAndDomMetrics()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <text id="label" x="10" y="25" dx="5" dy="7" font-family="sans-serif" font-size="10" inline-size="120" textLength="60">ABC</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var sharedSucceeded = InvokeTryCreateSharedInlineSizeTextLayoutResult(svgText, 10f, 25f, viewport, assetLoader, out var layout);
        var metricsSucceeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(sharedSucceeded);
        Assert.True(metricsSucceeded);
        Assert.NotNull(layout);
        Assert.NotNull(metrics);
        var firstPlacement = layout!.Lines[0].PositionedSpans[0].Placements[0].Point;
        Assert.Equal(15f, firstPlacement.X, 3);
        Assert.Equal(32f, firstPlacement.Y, 3);
        Assert.Equal(firstPlacement, layout.DomClusters[0].StartPoint);
        Assert.Equal(metrics!.GetStartPositionOfChar(0), layout.DomClusters[0].StartPoint);
    }

    [Fact]
    public void TryCreateTextContentMetrics_InlineSizeTextLengthRootDxIsAppliedOnce()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <text id="label" y="25" dx="5" font-family="sans-serif" font-size="10" inline-size="120" textLength="60">ABC</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(5f, metrics!.GetStartPositionOfChar(0).X, 3);
        Assert.Equal(25f, metrics.GetStartPositionOfChar(0).Y, 3);
        Assert.Equal(60f, metrics.ComputedTextLength, 3);
    }

    [Fact]
    public void TryCreateSharedInlineSizeTextLayoutResult_DomMetricsUsesClusterOwnedSubstringLength()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <text id="label" x="10" y="25" font-family="sans-serif" font-size="10" inline-size="120" textLength="60" lengthAdjust="spacing">A&#x0301;B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateSharedInlineSizeTextLayoutResult(svgText, 10f, 25f, viewport, assetLoader, out var layout);

        Assert.True(succeeded);
        Assert.NotNull(layout);
        Assert.Equal(3, layout!.DomNumberOfChars);
        Assert.Equal(layout.DomComputedTextLength, layout.GetDomSubStringLength(0, layout.DomNumberOfChars), 3);
        Assert.Equal(layout.GetDomSubStringLength(0, 1), layout.GetDomSubStringLength(1, 1), 3);
        Assert.Equal(layout.DomComputedTextLength, layout.GetDomSubStringLength(1, int.MaxValue), 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeInsideUsesShapeBounds()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="130" viewBox="0 0 140 130">
              <defs>
                <rect id="shape" x="40" y="20" width="15" height="80" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(40f, firstStart.X, 3);
        Assert.Equal(40f, secondStart.X, 3);
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected shape-inside DOM metrics to use the wrapped shape lines, but Y values were {firstStart.Y} and {secondStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_MultipleShapeSubtractUsesOpenFragment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <defs>
                <rect id="shape" x="10" y="20" width="140" height="80" />
                <rect id="left" x="10" y="20" width="50" height="36" />
                <rect id="right" x="90" y="20" width="60" height="36" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)" shape-subtract="url(#left) url(#right)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.InRange(metrics.FirstStartPosition.X, 60f, 90f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeSubtractFlowsAcrossSameLineFragments()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <rect id="shape" x="10" y="20" width="100" height="80" />
                <rect id="middle" x="30" y="20" width="60" height="36" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)" shape-subtract="url(#middle)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
        Assert.True(secondStart.X >= 90f, $"Expected wrapped shape text to continue in the second same-line fragment, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_ShapeSubtractIgnoresInvalidEntries()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <defs>
                <rect id="shape" x="10" y="20" width="140" height="80" />
                <rect id="left" x="10" y="20" width="60" height="36" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)" shape-subtract="url(#missing) url(#left)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.True(metrics.FirstStartPosition.X >= 70f, $"Expected valid subtract entry to remain active after an invalid entry, but X was {metrics.FirstStartPosition.X}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_MultipleShapeInsideFlowsOverflowIntoNextShape()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <defs>
                <rect id="first" x="10" y="20" width="15" height="20" />
                <rect id="second" x="80" y="20" width="80" height="20" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#first) url(#second)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(80f, secondStart.X, 3);
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
    }

    [Fact]
    public void TryCreateTextContentMetrics_NonRectangularShapeInsideUsesSampledFragment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="140" viewBox="0 0 160 140">
              <defs>
                <path id="shape" d="M60 20 L110 110 L10 110 Z" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.True(firstStart.X > secondStart.X, $"Expected upper triangle row to start farther right than lower row, but X values were {firstStart.X} and {secondStart.X}.");
        Assert.True(secondStart.Y > firstStart.Y + 8f, $"Expected non-rectangular shape DOM metrics to wrap to a later row, but Y values were {firstStart.Y} and {secondStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_SharedLayoutCombinesBidiLineBreakAndShapeExclusion()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="140" viewBox="0 0 180 140">
              <defs>
                <rect id="shape" x="20" y="20" width="86" height="90" />
                <rect id="subtract" x="20" y="20" width="42" height="34" />
              </defs>
              <text id="label"
                    font-family="sans-serif"
                    font-size="10"
                    shape-inside="url(#shape)"
                    shape-subtract="url(#subtract)"
                    direction="rtl"
                    unicode-bidi="embed"
                    line-break="anywhere">A-B &#x05D0;&#x05D1; C</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.True(metrics!.NumberOfChars >= 6, $"Expected at least six visible shared-layout characters, but got {metrics.NumberOfChars}.");

        var firstStart = metrics.GetStartPositionOfChar(0);
        var hyphenStart = metrics.GetStartPositionOfChar(1);
        var hebrewStart = metrics.GetStartPositionOfChar(3);
        var lastStart = metrics.GetStartPositionOfChar(metrics.NumberOfChars - 1);

        Assert.True(firstStart.X >= 62f, $"Expected first RTL line to honor the first-row shape-subtract fragment, but X was {firstStart.X}.");
        Assert.Equal(firstStart.Y, hyphenStart.Y, 3);
        Assert.True(hebrewStart.Y > firstStart.Y + 8f || lastStart.Y > firstStart.Y + 8f, $"Expected combined bidi/line-break/shape layout to wrap after the first fragment, but positions were {firstStart}, {hebrewStart}, and {lastStart}.");

        var firstExtent = metrics.GetExtentOfChar(0);
        var firstHitPoint = new SKPoint(
            (firstExtent.Left + firstExtent.Right) * 0.5f,
            (firstExtent.Top + firstExtent.Bottom) * 0.5f);
        Assert.Equal(0, metrics.GetCharNumAtPosition(firstHitPoint));
        Assert.True(metrics.GetSubStringLength(0, metrics.NumberOfChars) > 0f);
    }

    [Fact]
    public void TryCreateTextContentMetrics_SharedLayoutTextPathPreservesClusterAndLengthMetrics()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="20" y="45" font-family="sans-serif" font-size="10" inline-size="140">
                <textPath id="path-run" href="#line" textLength="90" lengthAdjust="spacingAndGlyphs">A&#x0301;B</textPath>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.NumberOfChars);
        Assert.Equal(90f, metrics.ComputedTextLength, 3);

        var baseStart = metrics.GetStartPositionOfChar(0);
        var combiningStart = metrics.GetStartPositionOfChar(1);
        var finalStart = metrics.GetStartPositionOfChar(2);

        Assert.Equal(baseStart.Y, combiningStart.Y, 3);
        Assert.Equal(baseStart.Y, finalStart.Y, 3);
        Assert.Equal(baseStart.X, combiningStart.X, 3);
        Assert.True(finalStart.X > combiningStart.X, $"Expected final textPath character to follow the textPath cluster metrics, but positions were {combiningStart} and {finalStart}.");
        Assert.Equal(90f, metrics.FullSubstringLength, 3);
        Assert.Equal(metrics.GetSubStringLength(0, 1), metrics.GetSubStringLength(1, 1), 3);

        var baseExtent = metrics.GetExtentOfChar(0);
        var clusterStartHitPoint = new SKPoint(
            baseExtent.Left + (baseExtent.Width * 0.25f),
            (baseExtent.Top + baseExtent.Bottom) * 0.5f);
        Assert.Equal(0, metrics.GetCharNumAtPosition(clusterStartHitPoint));
    }

    [Fact]
    public void TryCreateTextContentMetrics_TextPathRotatesClusterExtent()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="140" viewBox="0 0 180 140">
              <path id="diagonal" d="M20,20 L120,120" />
              <text id="label" x="0" y="0" font-family="sans-serif" font-size="10" inline-size="160">
                <textPath href="#diagonal">A</textPath>
              </text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.NumberOfChars);
        Assert.InRange(metrics.GetRotationOfChar(0), 40f, 50f);

        var extent = metrics.GetExtentOfChar(0);
        Assert.True(extent.Width > 12f, $"Expected rotated textPath extent width to include rotated glyph bounds, but extent was {extent}.");
        Assert.True(extent.Height > 12f, $"Expected rotated textPath extent height to include rotated glyph bounds, but extent was {extent}.");
        var hitPoint = new SKPoint(
            (extent.Left + extent.Right) * 0.5f,
            (extent.Top + extent.Bottom) * 0.5f);
        Assert.Equal(0, metrics.GetCharNumAtPosition(hitPoint));
    }

    [Fact]
    public void SvgTextLayoutPlanner_PreservesClustersAndSuppressesClusterBreaks()
    {
        var plan = CreateTextLayoutPlan(
            "A\u0301B",
            overflowWrapAnywhere: true);

        var clusters = GetPlanItems(plan, "Clusters");
        Assert.Equal(2, clusters.Count);
        Assert.Equal("A\u0301", GetPlanProperty<string>(clusters[0], "Text"));
        Assert.Equal(2, GetPlanProperty<int>(clusters[0], "CodepointCount"));
        Assert.Equal("B", GetPlanProperty<string>(clusters[1], "Text"));

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.DoesNotContain(
            breakOpportunities,
            opportunity => GetPlanProperty<int>(opportunity, "BeforeCodepointIndex") == 0);
        if (breakOpportunities.Count > 0)
        {
            Assert.Contains(
                breakOpportunities,
                opportunity =>
                    GetPlanProperty<int>(opportunity, "BeforeCodepointIndex") >= 1 &&
                    GetPlanEnumName(opportunity, "Kind") == "Soft");
        }
    }

    [Fact]
    public void SvgTextPathLayoutPlanner_TinyScaledPathReturnsUnitTangent()
    {
        var samples = CreateTextPathPlannerSamples(
            (new SKPoint(0f, 0f), 0f, true, false),
            (new SKPoint(0.0001f, 0f), 100f, false, false));

        var succeeded = InvokeTryGetTextPathPlannerPointAndTangent(samples, 50f, out var point, out var tangent);

        Assert.True(succeeded);
        Assert.Equal(0.00005f, point.X, 8);
        Assert.Equal(0f, point.Y, 8);
        Assert.Equal(1f, tangent.X, 6);
        Assert.Equal(0f, tangent.Y, 6);
    }

    [Fact]
    public void TryCreateStretchedTextPathClusters_MergesShapedClustersInsideComplexTextElement()
    {
        const string text = "\u0915\u093fB";
        var document = CreateDocument(text, 16);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var paint = CreateTextPaint(svgText, viewport);
        var assetLoader = new SplitComplexTextElementClusterAssetLoader();

        var succeeded = InvokeTryCreateStretchedTextPathClusters(
            svgText,
            text,
            paint,
            isRightToLeft: false,
            viewport,
            assetLoader,
            out var clusters);

        Assert.True(succeeded);
        Assert.Equal(2, clusters.Count);
        Assert.Equal("\u0915\u093f", GetPlanProperty<string>(clusters[0], "Text"));
        Assert.Equal("B", GetPlanProperty<string>(clusters[1], "Text"));
        Assert.Equal(0f, GetPlanProperty<float>(clusters[0], "NaturalOffset"), 3);
        Assert.Equal(10f, GetPlanProperty<float>(clusters[0], "NaturalAdvance"), 3);
        Assert.Equal(10f, GetPlanProperty<float>(clusters[1], "NaturalOffset"), 3);
    }

    [Fact]
    public void TryCreateStretchClusterPlan_ScalesClusterOffsetsForSpacingAndGlyphs()
    {
        var plannerClusters = CreateStretchClusterInputs(
            (0f, 10f, 10f),
            (10f, 10f, 0f));

        var succeeded = InvokeTryCreateStretchClusterPlan(
            plannerClusters,
            naturalAdvance: 20f,
            targetAdvance: 90f,
            distributeTextLengthGap: false,
            scaleGlyphsAndSpacing: true,
            out var plan);

        Assert.True(succeeded);
        Assert.NotNull(plan);
        Assert.Equal(90f, GetPlanProperty<float>(plan!, "AdjustedAdvance"), 3);

        var placements = GetPlanItems(plan!, "Placements");
        Assert.Equal(2, placements.Count);
        Assert.Equal(0f, GetPlanProperty<float>(placements[0], "AdjustedOffset"), 3);
        Assert.Equal(60f, GetPlanProperty<float>(placements[1], "AdjustedOffset"), 3);
        Assert.Equal(3f, GetPlanProperty<float>(placements[0], "ScaleX"), 3);
        Assert.Equal(3f, GetPlanProperty<float>(placements[1], "ScaleX"), 3);
    }

    [Fact]
    public void TryCreateStretchedTextPathRunPath_ScalesClusterPathsWithSpacingAndGlyphs()
    {
        const string text = "A B";
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="90" viewBox="0 0 220 90">
              <path id="line" d="M10 45 L210 45" />
              <text font-family="sans-serif" font-size="10">
                <textPath id="stretch-path"
                          href="#line"
                          method="stretch"
                          textLength="100"
                          lengthAdjust="spacingAndGlyphs"
                          letter-spacing="10">A B</textPath>
              </text>
            </svg>
            """);
        var svgTextPath = document.Descendants().OfType<SvgTextPath>().Single(static element => element.ID == "stretch-path");
        var viewport = GetDocumentViewport(document);
        var pathSamples = CreateCompilerPathSamples(
            (new SKPoint(0f, 0f), 0f, true, false),
            (new SKPoint(200f, 0f), 200f, false, false));
        var assetLoader = new RectGlyphPathAssetLoader();

        var succeeded = InvokeTryCreateStretchedTextPathRunPath(
            svgTextPath,
            svgTextPath,
            text,
            pathSamples,
            pathLength: 200f,
            viewport,
            assetLoader,
            out var stretchedPath,
            out var totalAdvance);

        Assert.True(succeeded);
        Assert.NotNull(stretchedPath);
        Assert.Equal(100f, totalAdvance, 3);
        Assert.True(
            stretchedPath!.Bounds.Width > 90f,
            $"Expected spacingAndGlyphs to scale the clustered stretch outlines close to textLength, but bounds were {stretchedPath.Bounds}.");
    }

    [Fact]
    public void TryCreateStretchedTextPathRunPath_SkipsPathlessColorFontClusterFallback()
    {
        const string text = "A\U0001F469\u200D\U0001F467B";
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <path id="line" d="M10 40 L170 40" />
              <text font-family="sans-serif" font-size="10">
                <textPath id="stretch-path"
                          href="#line"
                          method="stretch"
                          textLength="60"
                          lengthAdjust="spacingAndGlyphs">A&#x1F469;&#x200D;&#x1F467;B</textPath>
              </text>
            </svg>
            """);
        var svgTextPath = document.Descendants().OfType<SvgTextPath>().Single(static element => element.ID == "stretch-path");
        var viewport = GetDocumentViewport(document);
        var pathSamples = CreateCompilerPathSamples(
            (new SKPoint(0f, 0f), 0f, true, false),
            (new SKPoint(160f, 0f), 160f, false, false));
        var assetLoader = new PathlessColorClusterAssetLoader();

        var succeeded = InvokeTryCreateStretchedTextPathRunPath(
            svgTextPath,
            svgTextPath,
            text,
            pathSamples,
            pathLength: 160f,
            viewport,
            assetLoader,
            out var stretchedPath,
            out var totalAdvance);

        Assert.True(succeeded);
        Assert.NotNull(stretchedPath);
        Assert.Equal(60f, totalAdvance, 3);
        Assert.True(
            stretchedPath!.Bounds.Width > 15f,
            $"Expected renderable neighboring clusters to survive a pathless color-font cluster, but bounds were {stretchedPath.Bounds}.");
    }

    [Fact]
    public void SvgTextLayoutPlanner_ExposesLogicalAndVisualBidiRuns()
    {
        var plan = CreateTextLayoutPlan(
            "A \u05D0\u05D1 B",
            direction: "RightToLeft",
            unicodeBidi: "Embed");

        Assert.True(GetPlanProperty<bool>(plan, "HasVisualReordering"));

        var logicalRuns = GetPlanItems(plan, "LogicalRuns");
        var visualRuns = GetPlanItems(plan, "VisualRuns");
        Assert.True(logicalRuns.Count > 1);
        Assert.Equal(logicalRuns.Count, visualRuns.Count);
        Assert.NotEqual(
            GetPlanProperty<int>(logicalRuns[0], "StartCharIndex"),
            GetPlanProperty<int>(visualRuns[0], "StartCharIndex"));
        Assert.Contains(visualRuns, run => GetPlanEnumName(run, "Direction") == "RightToLeft");
    }

    [Fact]
    public void SvgTextLayoutPlanner_CreatesLineScopedVisualBidiRuns()
    {
        var plan = CreateTextLayoutPlan(
            "A\u05D0\u05D1B\u05D2\u05D3",
            direction: "RightToLeft",
            unicodeBidi: "Embed",
            lineBreakAnywhere: true);

        var firstLineRuns = InvokeCreateLineScopedVisualRuns(plan, 0, 3);
        var secondLineRuns = InvokeCreateLineScopedVisualRuns(plan, 3, 3);

        var firstLineTexts = firstLineRuns.Select(run => GetPlanProperty<string>(run, "Text")).ToArray();
        var secondLineTexts = secondLineRuns.Select(run => GetPlanProperty<string>(run, "Text")).ToArray();

        Assert.True(Array.IndexOf(firstLineTexts, "\u05D0\u05D1") >= 0, $"Expected first line Hebrew visual run, but got {string.Join("|", firstLineTexts)}.");
        Assert.True(Array.IndexOf(secondLineTexts, "\u05D2\u05D3") >= 0, $"Expected second line Hebrew visual run, but got {string.Join("|", secondLineTexts)}.");
        Assert.True(Array.IndexOf(firstLineTexts, "\u05D0\u05D1") < Array.IndexOf(firstLineTexts, "A"), $"Expected first line visual order Hebrew before Latin, but got {string.Join("|", firstLineTexts)}.");
        Assert.True(Array.IndexOf(secondLineTexts, "\u05D2\u05D3") < Array.IndexOf(secondLineTexts, "B"), $"Expected second line visual order Hebrew before Latin, but got {string.Join("|", secondLineTexts)}.");
    }

    [Fact]
    public void SvgTextLayoutPlanner_PlainTextResolvesBaseDirectionPerLine()
    {
        var plan = CreateTextLayoutPlan(
            "A\u05D0\u05D1\n\u05D2\u05D3B",
            direction: "LeftToRight",
            unicodeBidi: "PlainText");

        var firstLineRuns = InvokeCreateLineScopedVisualRuns(plan, 0, 3);
        var secondLineRuns = InvokeCreateLineScopedVisualRuns(plan, 4, 3);

        var firstLineTexts = firstLineRuns.Select(run => GetPlanProperty<string>(run, "Text")).ToArray();
        var secondLineTexts = secondLineRuns.Select(run => GetPlanProperty<string>(run, "Text")).ToArray();

        Assert.True(Array.IndexOf(firstLineTexts, "A") >= 0, $"Expected first plaintext line Latin run, but got {string.Join("|", firstLineTexts)}.");
        Assert.True(Array.IndexOf(firstLineTexts, "\u05D0\u05D1") >= 0, $"Expected first plaintext line Hebrew run, but got {string.Join("|", firstLineTexts)}.");
        Assert.True(Array.IndexOf(secondLineTexts, "\u05D2\u05D3") >= 0, $"Expected second plaintext line Hebrew run, but got {string.Join("|", secondLineTexts)}.");
        Assert.True(Array.IndexOf(secondLineTexts, "B") >= 0, $"Expected second plaintext line Latin run, but got {string.Join("|", secondLineTexts)}.");
        Assert.True(Array.IndexOf(firstLineTexts, "A") < Array.IndexOf(firstLineTexts, "\u05D0\u05D1"), $"Expected first plaintext line to resolve LTR, but got {string.Join("|", firstLineTexts)}.");
        Assert.True(Array.IndexOf(secondLineTexts, "B") < Array.IndexOf(secondLineTexts, "\u05D2\u05D3"), $"Expected second plaintext line to resolve RTL, but got {string.Join("|", secondLineTexts)}.");
    }

    [Fact]
    public void SvgTextLayoutPlanner_ReportsPrioritizedBreakOpportunities()
    {
        var plan = CreateTextLayoutPlan("A B\u200BC");

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.Contains(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Whitespace" &&
                GetPlanEnumName(opportunity, "Priority") == "Whitespace" &&
                !GetPlanProperty<bool>(opportunity, "ConsumesCodepoint"));
        Assert.Contains(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Invisible" &&
                GetPlanEnumName(opportunity, "Priority") == "Natural" &&
                GetPlanProperty<bool>(opportunity, "ConsumesCodepoint") &&
                GetPlanProperty<int>(opportunity, "BreakCodepointIndex") == 3);
    }

    [Fact]
    public void SvgTextLayoutPlanner_ReportsSoftHyphenAsConsumingInvisibleBreak()
    {
        var plan = CreateTextLayoutPlan("A\u00ADB");

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.Contains(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Invisible" &&
                GetPlanEnumName(opportunity, "Priority") == "Natural" &&
                GetPlanProperty<bool>(opportunity, "ConsumesCodepoint") &&
                GetPlanProperty<int>(opportunity, "BeforeCodepointIndex") == 0 &&
                GetPlanProperty<int>(opportunity, "AfterCodepointIndex") == 2 &&
                GetPlanProperty<int>(opportunity, "BreakCodepointIndex") == 1);
    }

    [Fact]
    public void SvgTextLayoutPlanner_ResolvesPairedBracketsWithEnclosedRtlText()
    {
        var plan = CreateTextLayoutPlan("\u05D0 (\u05D1\u05D2) A");

        var logicalRuns = GetPlanItems(plan, "LogicalRuns");
        Assert.Contains(
            logicalRuns,
            run =>
                GetPlanProperty<string>(run, "Text") == "\u05D0 (\u05D1\u05D2)" &&
                GetPlanEnumName(run, "Direction") == "RightToLeft");
    }

    [Fact]
    public void SvgTextLayoutPlanner_IsolatesDoNotLeakIntoOuterNeutralResolution()
    {
        var plan = CreateTextLayoutPlan("\u05D0?\u2066A\u2069\u05D1");

        var logicalRuns = GetPlanItems(plan, "LogicalRuns");
        Assert.Contains(
            logicalRuns,
            run =>
                GetPlanProperty<string>(run, "Text") == "\u05D0?" &&
                GetPlanEnumName(run, "Direction") == "RightToLeft");
    }

    [Fact]
    public void SvgTextLayoutPlanner_NestedBidiOverrideSpanUsesElementDirection()
    {
        var plan = CreateTextLayoutPlanFromRuns(
            [
                ("A ", "LeftToRight", "Normal"),
                ("BCD", "RightToLeft", "BidiOverride"),
                (" Z", "LeftToRight", "Normal")
            ]);

        var visualRunTexts = GetPlanItems(plan, "VisualRuns")
            .Select(run => GetPlanProperty<string>(run, "Text"))
            .ToArray();

        var dIndex = Array.IndexOf(visualRunTexts, "D");
        var cIndex = Array.IndexOf(visualRunTexts, "C");
        var bIndex = Array.IndexOf(visualRunTexts, "B");
        Assert.True(dIndex >= 0 && cIndex >= 0 && bIndex >= 0, $"Expected nested RTL override to split BCD into visual codepoint runs, but got {string.Join("|", visualRunTexts)}.");
        Assert.True(dIndex < cIndex && cIndex < bIndex, $"Expected nested RTL override visual order D,C,B, but got {string.Join("|", visualRunTexts)}.");
    }

    [Fact]
    public void SvgTextBidiResolver_PreservesSameDirectionExplicitEmbeddingSpan()
    {
        var visualRuns = InvokeCreateVisualBidiRuns(
            "ZA\u05D0B",
            "LeftToRight",
            "Embed",
            [(1, 2, "LeftToRight", "Embed")]);

        Assert.Contains(
            visualRuns,
            run => GetPlanProperty<int>(run, "Level") >= 2);
    }

    [Fact]
    public void DrawTextRuns_RtlVisualTypefaceSpansDrawOnceInVisualOrder()
    {
        const string text = "ABC \u05D0\u05D1\u05D2 DEF";
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80">
              <text id="label" x="10" y="40" font-size="20" direction="rtl" unicode-bidi="embed">ABC &#x05D0;&#x05D1;&#x05D2; DEF</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var loader = new VisualOrderTypefaceSpanAssetLoader();

        var draws = InvokeDrawTextRuns(svgText, text, SKRect.Create(0f, 0f, 240f, 80f), loader)
            .Select(static command => command.Text)
            .ToArray();

        Assert.Equal(["DEF", "\u05D0\u05D1\u05D2", "ABC"], draws);
    }

    [Fact]
    public void SvgTextLayoutPlanner_NestedIsolateSpanKeepsOuterNeutralPunctuation()
    {
        var plan = CreateTextLayoutPlanFromRuns(
            [
                ("\u05D0?", "RightToLeft", "Normal"),
                ("A", "LeftToRight", "Isolate"),
                ("\u05D1", "RightToLeft", "Normal")
            ],
            paragraphDirection: "RightToLeft");

        var logicalRuns = GetPlanItems(plan, "LogicalRuns");
        Assert.Contains(
            logicalRuns,
            run =>
                GetPlanProperty<string>(run, "Text") == "\u05D0?" &&
                GetPlanEnumName(run, "Direction") == "RightToLeft");
    }

    [Fact]
    public void SvgTextLayoutPlanner_KeepsApostropheInsideAlphabeticWord()
    {
        var plan = CreateTextLayoutPlan(
            "can't",
            overflowWrapAnywhere: false,
            wordBreakBreakAll: false,
            lineBreakAnywhere: false);

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.DoesNotContain(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Soft" &&
                GetPlanProperty<int>(opportunity, "BeforeCodepointIndex") == 3);
    }

    [Fact]
    public void SvgTextLayoutPlanner_KeepsNumericSlashExpressionTogether()
    {
        var plan = CreateTextLayoutPlan("12/34");

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.DoesNotContain(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Soft" &&
                GetPlanProperty<int>(opportunity, "BeforeCodepointIndex") is 1 or 2);
    }

    [Fact]
    public void SvgTextLayoutPlanner_LineBreakLooseAllowsSmallKanaBoundary()
    {
        var plan = CreateTextLayoutPlan(
            "\u3042\u3041",
            lineBreakLoose: true);

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.Contains(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Soft" &&
                GetPlanProperty<int>(opportunity, "BeforeCodepointIndex") == 0);
    }

    [Theory]
    [InlineData("A\u00A0B", true, false, false)]
    [InlineData("A\u00A0B", false, true, false)]
    [InlineData("A\u00A0B", false, false, true)]
    [InlineData("A\u2060B", false, false, true)]
    public void SvgTextLayoutPlanner_CharacterBreakModesHonorHardNoBreakCodepoints(
        string text,
        bool overflowWrapAnywhere,
        bool wordBreakBreakAll,
        bool lineBreakAnywhere)
    {
        var plan = CreateTextLayoutPlan(
            text,
            overflowWrapAnywhere: overflowWrapAnywhere,
            wordBreakBreakAll: wordBreakBreakAll,
            lineBreakAnywhere: lineBreakAnywhere);

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.DoesNotContain(
            breakOpportunities,
            opportunity => GetPlanEnumName(opportunity, "Kind") is "Soft" or "Whitespace");
    }

    [Fact]
    public void SvgTextLayoutPlanner_DoesNotCreateNaturalBreaksInsideComplexContextText()
    {
        var plan = CreateTextLayoutPlan("\u0E20\u0E32\u0E29\u0E32");

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.DoesNotContain(
            breakOpportunities,
            opportunity => GetPlanEnumName(opportunity, "Kind") == "Soft");
    }

    [Fact]
    public void SvgTextLayoutPlanner_AllowsEmergencyBreaksInsideComplexContextText()
    {
        var plan = CreateTextLayoutPlan(
            "\u0E20\u0E32\u0E29\u0E32",
            overflowWrapAnywhere: true);

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.Contains(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Soft" &&
                GetPlanEnumName(opportunity, "Priority") == "Emergency");
    }

    [Fact]
    public void SvgTextLayoutPlanner_DoesNotBreakInsideEmojiTagSequence()
    {
        var plan = CreateTextLayoutPlan(
            "\U0001F3F4\U000E0067\U000E0062\U000E007FZ",
            overflowWrapAnywhere: true);

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.DoesNotContain(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Soft" &&
                GetPlanProperty<int>(opportunity, "BeforeCodepointIndex") < 3);
    }

    [Fact]
    public void SvgTextLayoutPlanner_DoesNotBreakInsideEmojiModifierSequence()
    {
        var plan = CreateTextLayoutPlan(
            "\U0001F44D\U0001F3FDZ",
            overflowWrapAnywhere: true);

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.DoesNotContain(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Soft" &&
                GetPlanProperty<int>(opportunity, "BeforeCodepointIndex") < 1);
    }

    [Fact]
    public void SvgTextLayoutPlanner_DoesNotBreakInsideEmojiZwJSequence()
    {
        var plan = CreateTextLayoutPlan(
            "\U0001F469\u200D\u2764\uFE0F\u200D\U0001F469Z",
            overflowWrapAnywhere: true);

        var breakOpportunities = GetPlanItems(plan, "BreakOpportunities");
        Assert.DoesNotContain(
            breakOpportunities,
            opportunity =>
                GetPlanEnumName(opportunity, "Kind") == "Soft" &&
                GetPlanProperty<int>(opportunity, "BeforeCodepointIndex") < 5);
    }

    [Theory]
    [InlineData("\U0001F44D\U0001F3FDZ", new[] { 0, 4 })]
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E007FZ", new[] { 0, 8 })]
    [InlineData("\U0001F469\u200D\u2764\uFE0F\u200D\U0001F469Z", new[] { 0, 8 })]
    public void SvgTextBoundaryResolver_PreservesEmojiGraphemeSequences(
        string text,
        int[] expectedStarts)
    {
        var starts = Assert.IsAssignableFrom<IEnumerable<int>>(
            s_getGraphemeClusterStartCharIndexesMethod.Invoke(s_textBoundaryResolver, [text]));

        Assert.Equal(expectedStarts, starts.ToArray());
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

    private static void VerifyPreservesTotalAdvance(string textContent)
    {
        var document = CreateDocument(textContent, 24);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var geometryBounds = GetDocumentViewport(document);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var codepoints = InvokeSplitCodepoints(textContent);
        var actual = InvokeMeasureNaturalCodepointAdvances(svgText, codepoints, geometryBounds, assetLoader);
        var expectedTotal = InvokeMeasureNaturalTextAdvance(svgText, textContent, geometryBounds, assetLoader);

        Assert.Equal(codepoints.Count, actual.Length);
        Assert.All(actual, static advance =>
        {
            Assert.True(float.IsFinite(advance), $"Expected finite codepoint advance, but got {advance}.");
            Assert.True(advance >= 0f, $"Expected non-negative codepoint advance, but got {advance}.");
        });
        Assert.Equal(expectedTotal, actual.Sum(), 3);
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

    private static object CreateTextLayoutPlan(
        string text,
        string direction = "LeftToRight",
        string unicodeBidi = "Normal",
        global::Svg.SvgWhiteSpace whiteSpace = global::Svg.SvgWhiteSpace.Normal,
        bool overflowWrapAnywhere = false,
        bool wordBreakBreakAll = false,
        bool wordBreakKeepAll = false,
        bool lineBreakAnywhere = false,
        bool lineBreakLoose = false,
        bool strictLineBreak = false)
    {
        var options = Activator.CreateInstance(
            s_svgTextLineBreakOptionsType,
            overflowWrapAnywhere,
            wordBreakBreakAll,
            wordBreakKeepAll,
            lineBreakAnywhere,
            lineBreakLoose,
            strictLineBreak);
        Assert.NotNull(options);

        var plan = s_createTextLayoutPlanMethod.Invoke(
            null,
            [
                text,
                Enum.Parse(s_svgTextDirectionType, direction),
                Enum.Parse(s_svgUnicodeBidiModeType, unicodeBidi),
                whiteSpace,
                options
            ]);
        Assert.NotNull(plan);
        return plan!;
    }

    private static object CreateTextLayoutPlanFromRuns(
        (string Text, string Direction, string UnicodeBidi)[] runs,
        string paragraphDirection = "LeftToRight",
        string paragraphUnicodeBidi = "Normal",
        global::Svg.SvgWhiteSpace whiteSpace = global::Svg.SvgWhiteSpace.Normal)
    {
        var options = Activator.CreateInstance(
            s_svgTextLineBreakOptionsType,
            false,
            false,
            false,
            false,
            false,
            false);
        Assert.NotNull(options);

        var paragraphStyle = CreateTextLayoutStyle(paragraphDirection, paragraphUnicodeBidi, whiteSpace, options);
        var inputRuns = Array.CreateInstance(s_svgTextLayoutInputRunType, runs.Length);
        var inputRunConstructor = s_svgTextLayoutInputRunType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 3);
        for (var i = 0; i < runs.Length; i++)
        {
            var style = CreateTextLayoutStyle(runs[i].Direction, runs[i].UnicodeBidi, whiteSpace, options);
            inputRuns.SetValue(
                inputRunConstructor.Invoke([i, runs[i].Text, style]),
                i);
        }

        var plan = s_createTextLayoutPlanFromRunsMethod.Invoke(null, [inputRuns, paragraphStyle]);
        Assert.NotNull(plan);
        return plan!;
    }

    private static List<object> InvokeCreateLineScopedVisualRuns(
        object plan,
        int startCharIndex,
        int length)
    {
        var visualRuns = s_createLineScopedVisualRunsMethod.Invoke(null, [plan, startCharIndex, length]);
        Assert.NotNull(visualRuns);
        return Assert.IsAssignableFrom<IEnumerable>(visualRuns)
            .Cast<object>()
            .ToList();
    }

    private static List<object> InvokeCreateVisualBidiRuns(
        string text,
        string direction,
        string unicodeBidi,
        params (int StartCharIndex, int Length, string Direction, string UnicodeBidi)[] spans)
    {
        var assembly = typeof(SvgSceneNode).Assembly;
        var resolverType = assembly.GetType("Svg.Skia.SvgTextBidiResolver")
            ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextBidiResolver.");
        var spanType = assembly.GetType("Svg.Skia.SvgTextBidiSpan")
            ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextBidiSpan.");
        var spanConstructor = spanType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 4);
        var spanArray = Array.CreateInstance(spanType, spans.Length);
        for (var i = 0; i < spans.Length; i++)
        {
            spanArray.SetValue(
                spanConstructor.Invoke(
                    [
                        spans[i].StartCharIndex,
                        spans[i].Length,
                        Enum.Parse(s_svgTextDirectionType, spans[i].Direction),
                        Enum.Parse(s_svgUnicodeBidiModeType, spans[i].UnicodeBidi)
                    ]),
                i);
        }

        var method = resolverType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == "CreateVisualRuns" && candidate.GetParameters().Length == 4);
        var visualRuns = method.Invoke(
            null,
            [
                text,
                Enum.Parse(s_svgTextDirectionType, direction),
                Enum.Parse(s_svgUnicodeBidiModeType, unicodeBidi),
                spanArray
            ]);
        Assert.NotNull(visualRuns);
        return Assert.IsAssignableFrom<IEnumerable>(visualRuns)
            .Cast<object>()
            .ToList();
    }

    private static object CreateTextLayoutStyle(
        string direction,
        string unicodeBidi,
        global::Svg.SvgWhiteSpace whiteSpace,
        object lineBreakOptions)
    {
        var constructor = s_svgTextLayoutStyleType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 4);
        return constructor.Invoke(
            [
                Enum.Parse(s_svgTextDirectionType, direction),
                Enum.Parse(s_svgUnicodeBidiModeType, unicodeBidi),
                whiteSpace,
                lineBreakOptions
            ]);
    }

    private static Array CreateTextPathPlannerSamples(params (SKPoint Point, float Distance, bool StartsSubpath, bool ClosesSubpath)[] samples)
    {
        var constructor = s_svgTextPathLayoutPlannerPathSampleType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 4);
        var result = Array.CreateInstance(s_svgTextPathLayoutPlannerPathSampleType, samples.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            result.SetValue(
                constructor.Invoke(
                    [
                        samples[i].Point,
                        samples[i].Distance,
                        samples[i].StartsSubpath,
                        samples[i].ClosesSubpath
                    ]),
                i);
        }

        return result;
    }

    private static Array CreateCompilerPathSamples(params (SKPoint Point, float Distance, bool StartsSubpath, bool ClosesSubpath)[] samples)
    {
        var constructor = s_svgSceneTextCompilerPathSampleType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 4);
        var result = Array.CreateInstance(s_svgSceneTextCompilerPathSampleType, samples.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            result.SetValue(
                constructor.Invoke(
                    [
                        samples[i].Point,
                        samples[i].Distance,
                        samples[i].StartsSubpath,
                        samples[i].ClosesSubpath
                    ]),
                i);
        }

        return result;
    }

    private static Array CreateStretchClusterInputs(params (float NaturalOffset, float NaturalAdvance, float SpacingAfter)[] clusters)
    {
        var constructor = s_svgTextPathLayoutPlannerStretchClusterInputType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 3);
        var result = Array.CreateInstance(s_svgTextPathLayoutPlannerStretchClusterInputType, clusters.Length);
        for (var i = 0; i < clusters.Length; i++)
        {
            result.SetValue(
                constructor.Invoke(
                    [
                        clusters[i].NaturalOffset,
                        clusters[i].NaturalAdvance,
                        clusters[i].SpacingAfter
                    ]),
                i);
        }

        return result;
    }

    private static bool InvokeTryGetTextPathPlannerPointAndTangent(
        Array samples,
        float distance,
        out SKPoint point,
        out SKPoint tangent)
    {
        object?[] args = [samples, distance, null, null];
        var succeeded = Assert.IsType<bool>(s_tryGetTextPathPlannerPointAndTangentMethod.Invoke(null, args));
        point = Assert.IsType<SKPoint>(args[2]);
        tangent = Assert.IsType<SKPoint>(args[3]);
        return succeeded;
    }

    private static bool InvokeTryCreateStretchClusterPlan(
        Array clusters,
        float naturalAdvance,
        float targetAdvance,
        bool distributeTextLengthGap,
        bool scaleGlyphsAndSpacing,
        out object? plan)
    {
        object?[] args =
        [
            clusters,
            naturalAdvance,
            targetAdvance,
            distributeTextLengthGap,
            scaleGlyphsAndSpacing,
            null
        ];
        var succeeded = Assert.IsType<bool>(s_tryCreateStretchClusterPlanMethod.Invoke(null, args));
        plan = succeeded ? args[5] : null;
        return succeeded;
    }

    private static List<object> GetPlanItems(object value, string propertyName)
    {
        var propertyValue = value.GetType().GetProperty(propertyName)!.GetValue(value);
        return Assert.IsAssignableFrom<IEnumerable>(propertyValue).Cast<object>().ToList();
    }

    private static T GetPlanProperty<T>(object value, string propertyName)
    {
        return Assert.IsType<T>(value.GetType().GetProperty(propertyName)!.GetValue(value));
    }

    private static string GetPlanEnumName(object value, string propertyName)
    {
        var enumValue = value.GetType().GetProperty(propertyName)!.GetValue(value);
        Assert.NotNull(enumValue);
        return enumValue!.ToString()!;
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
            null,
            null,
            default(SKRect),
            null
        };

        return Assert.IsType<bool>(s_tryCompileSequentialTextMethod.Invoke(null, args));
    }

    private static IReadOnlyList<DrawTextCanvasCommand> InvokeDrawTextRuns(
        SvgTextBase svgTextBase,
        string text,
        SKRect viewport,
        ISvgAssetLoader assetLoader)
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(viewport);
        var paint = new SKPaint();
        _ = s_drawTextRunsMethod.Invoke(
            null,
            [
                svgTextBase,
                text,
                10f,
                40f,
                viewport,
                paint,
                canvas,
                assetLoader,
                null
            ]);

        return recorder.EndRecording().Commands?.OfType<DrawTextCanvasCommand>().ToArray()
            ?? Array.Empty<DrawTextCanvasCommand>();
    }

    private static SKRect InvokeSvgFontLayoutBounds(
        SvgTextBase svgTextBase,
        string text,
        float textSize,
        float baselineY)
    {
        var paint = new SKPaint { TextSize = textSize };
        var args = new object?[]
        {
            svgTextBase,
            text,
            paint,
            null,
            false,
            null
        };

        Assert.True(Assert.IsType<bool>(s_tryGetSvgFontLayoutMethod.Invoke(null, args)));
        Assert.NotNull(args[5]);
        var layout = args[5]!;
        return Assert.IsType<SKRect>(layout.GetType().GetMethod("GetBounds")!.Invoke(layout, [0f, baselineY]));
    }

    private static float GetVerticalCenter(SKRect bounds)
    {
        return (bounds.Top + bounds.Bottom) * 0.5f;
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

    private static bool InvokeTryCreateTextContentMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out TextContentMetricsSnapshot? snapshot)
    {
        var args = new object?[]
        {
            svgTextBase,
            viewport,
            assetLoader,
            null
        };

        var succeeded = Assert.IsType<bool>(s_tryCreateTextContentMetricsMethod.Invoke(null, args));
        if (!succeeded)
        {
            snapshot = null;
            return false;
        }

        Assert.NotNull(args[3]);
        var metrics = args[3]!;
        snapshot = new TextContentMetricsSnapshot(metrics);
        return true;
    }

    private static bool InvokeTryCreateSharedInlineSizeTextLayoutResult(
        SvgTextBase svgTextBase,
        float currentX,
        float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SharedTextLayoutSnapshot? snapshot)
    {
        var args = new object?[]
        {
            svgTextBase,
            currentX,
            currentY,
            viewport,
            viewport,
            assetLoader,
            true,
            null,
            null,
            0f,
            0f
        };

        var succeeded = Assert.IsType<bool>(s_tryCreateSharedInlineSizeTextLayoutResultMethod.Invoke(null, args));
        if (!succeeded)
        {
            snapshot = null;
            return false;
        }

        Assert.NotNull(args[7]);
        snapshot = new SharedTextLayoutSnapshot(
            args[7]!,
            Assert.IsType<float>(args[9]),
            Assert.IsType<float>(args[10]));
        return true;
    }

    private static bool InvokeTryCreateCssShapeAlphaPath(byte[] encodedData, SKRect referenceBox, float threshold, out SKPath? path)
    {
        var args = new object?[]
        {
            encodedData,
            referenceBox,
            threshold,
            null
        };

        var succeeded = Assert.IsType<bool>(s_tryCreateCssShapeAlphaPathMethod.Invoke(null, args));
        if (!succeeded)
        {
            path = null;
            return false;
        }

        Assert.NotNull(args[3]);
        path = Assert.IsType<SKPath>(args[3]);
        return true;
    }

    private static bool InvokeTryCreateStretchedTextPathClusters(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        bool isRightToLeft,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out List<object> clusters)
    {
        var args = new object?[]
        {
            svgTextBase,
            text,
            paint,
            isRightToLeft,
            geometryBounds,
            assetLoader,
            null
        };

        var succeeded = Assert.IsType<bool>(s_tryCreateStretchedTextPathClustersMethod.Invoke(null, args));
        clusters = succeeded && args[6] is IEnumerable clusterItems
            ? clusterItems.Cast<object>().ToList()
            : [];
        return succeeded;
    }

    private static bool InvokeTryCreateStretchedTextPathRunPath(
        SvgTextBase svgTextBase,
        SvgTextBase lengthSource,
        string text,
        Array pathSamples,
        float pathLength,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SKPath? stretchedPath,
        out float totalAdvance)
    {
        object?[] args =
        [
            svgTextBase,
            lengthSource,
            text,
            0f,
            0f,
            pathSamples,
            pathLength,
            false,
            viewport,
            viewport,
            assetLoader,
            null,
            null,
            null,
            0f,
            null
        ];

        var succeeded = Assert.IsType<bool>(s_tryCreateStretchedTextPathRunPathMethod.Invoke(null, args));
        stretchedPath = succeeded ? Assert.IsType<SKPath>(args[12]) : null;
        totalAdvance = Assert.IsType<float>(args[14]);
        return succeeded;
    }

    private static SKPaint CreateTextPaint(SvgTextBase svgTextBase, SKRect viewport)
    {
        _ = viewport;
        var paint = new SKPaint
        {
            TextAlign = SKTextAlign.Left,
            TextSize = svgTextBase.FontSize.Value > 0f ? svgTextBase.FontSize.Value : 16f,
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        return paint;
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

    private static bool TryReadPngDimension(byte[] data, int offset, out int value)
    {
        value = 0;
        if (data.Length < offset + 4 ||
            data[0] != 137 ||
            data[1] != 80 ||
            data[2] != 78 ||
            data[3] != 71)
        {
            return false;
        }

        value = (data[offset] << 24) |
                (data[offset + 1] << 16) |
                (data[offset + 2] << 8) |
                data[offset + 3];
        return value > 0;
    }

    private static SKImage LoadTestImage(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var data = memory.ToArray();
        return new SKImage
        {
            Data = data,
            Width = TryReadPngDimension(data, 16, out var width) ? width : 0f,
            Height = TryReadPngDimension(data, 20, out var height) ? height : 0f
        };
    }

    private static bool TryDecodeNativeImageAlpha(SKImage image, out int width, out int height, out byte[] alpha)
    {
        width = 0;
        height = 0;
        alpha = Array.Empty<byte>();
        if (image.Data is null || image.Data.Length == 0)
        {
            return false;
        }

        using var skImage = SkiaSharp.SKImage.FromEncodedData(image.Data);
        if (skImage is null)
        {
            return false;
        }

        using var bitmap = SkiaSharp.SKBitmap.FromImage(skImage);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return false;
        }

        width = bitmap.Width;
        height = bitmap.Height;
        alpha = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                alpha[(y * width) + x] = bitmap.GetPixel(x, y).Alpha;
            }
        }

        return true;
    }

    private static string CreateOpaqueEncodedImageDataUri(SkiaSharp.SKEncodedImageFormat format, string mimeType)
    {
        using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(4, 4, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        bitmap.Erase(new SkiaSharp.SKColor(0x20, 0x80, 0xe0, 0xff));
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        return $"data:{mimeType};base64,{Convert.ToBase64String(data.ToArray())}";
    }

    private static string CreateSplitAlphaPngDataUri(byte leftAlpha, byte rightAlpha)
    {
        using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(4, 4, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul));
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                bitmap.SetPixel(x, y, new SkiaSharp.SKColor(0x20, 0x80, 0xe0, x < 2 ? leftAlpha : rightAlpha));
            }
        }

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return $"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
    }

    private static byte[] CreateIndexedTransparencyPng()
    {
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        WritePngChunk(
            png,
            "IHDR",
            [
                0, 0, 0, 2,
                0, 0, 0, 1,
                8,
                3,
                0,
                0,
                0
            ]);
        WritePngChunk(png, "PLTE", [0, 0, 0, 255, 255, 255]);
        WritePngChunk(png, "tRNS", [0, 255]);
        WritePngChunk(png, "IDAT", CompressZLib([0, 0, 1]));
        WritePngChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static byte[] CompressZLib(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    private static void WritePngChunk(Stream stream, string type, byte[] data)
    {
        WriteBigEndianInt32(stream, data.Length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes, 0, typeBytes.Length);
        stream.Write(data, 0, data.Length);
        WriteBigEndianInt32(stream, 0);
    }

    private static void WriteBigEndianInt32(Stream stream, int value)
    {
        stream.WriteByte((byte)((value >> 24) & 0xff));
        stream.WriteByte((byte)((value >> 16) & 0xff));
        stream.WriteByte((byte)((value >> 8) & 0xff));
        stream.WriteByte((byte)(value & 0xff));
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

        public SKImage LoadImage(Stream stream) => LoadTestImage(stream);

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

        public SKImage LoadImage(Stream stream) => LoadTestImage(stream);

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

    private sealed class VisualOrderTypefaceSpanAssetLoader : ISvgAssetLoader
    {
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream) => LoadTestImage(stream);

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        {
            var spans = new List<TypefaceSpan>();
            foreach (var run in SplitStrongRuns(text ?? string.Empty))
            {
                spans.Add(new TypefaceSpan(run, run.Length * 10f, _typeface));
            }

            return spans;
        }

        public SKFontMetrics GetFontMetrics(SKPaint paint) => default;

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        {
            bounds = default;
            return SplitStrongRuns(text ?? string.Empty).Sum(static run => run.Length * 10f);
        }

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y) => null;

        private static IEnumerable<string> SplitStrongRuns(string text)
        {
            var builder = new StringBuilder();
            StrongRunDirection? currentDirection = null;

            foreach (var character in text)
            {
                var direction = GetStrongRunDirection(character);
                if (direction is null)
                {
                    continue;
                }

                if (currentDirection is { } &&
                    currentDirection != direction)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                currentDirection = direction;
                builder.Append(character);
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }
        }

        private static StrongRunDirection? GetStrongRunDirection(char character)
        {
            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                return StrongRunDirection.LeftToRight;
            }

            if (character is >= '\u0590' and <= '\u05FF')
            {
                return StrongRunDirection.RightToLeft;
            }

            return null;
        }

        private enum StrongRunDirection
        {
            LeftToRight,
            RightToLeft
        }
    }

    private sealed class SplitComplexTextElementClusterAssetLoader : ISvgAssetLoader, ISvgTextRunTypefaceResolver, ISvgTextGlyphRunResolver
    {
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream) => LoadTestImage(stream);

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        {
            var resolvedText = text ?? string.Empty;
            return
            [
                new TypefaceSpan(resolvedText, GetAdvance(resolvedText), _typeface)
            ];
        }

        public SKFontMetrics GetFontMetrics(SKPaint paint)
        {
            var size = paint.TextSize > 0f ? paint.TextSize : 10f;
            return new SKFontMetrics
            {
                Ascent = -size * 0.8f,
                Descent = size * 0.2f,
                Top = -size * 0.8f,
                Bottom = size * 0.2f,
                Leading = 0f
            };
        }

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        {
            var advance = GetAdvance(text ?? string.Empty);
            var metrics = GetFontMetrics(paint);
            bounds = new SKRect(0f, metrics.Ascent, advance, metrics.Descent);
            return advance;
        }

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y) => null;

        public SKTypeface? FindRunTypeface(string? text, SKPaint paintPreferredTypeface) => _typeface;

        public bool TryShapeGlyphRun(string? text, SKPaint paint, out ShapedGlyphRun shapedRun)
        {
            if (text != "\u0915\u093fB")
            {
                shapedRun = default;
                return false;
            }

            shapedRun = new ShapedGlyphRun(
                [1, 2, 3],
                [new SKPoint(0f, 0f), new SKPoint(0f, 0f), new SKPoint(10f, 0f)],
                [0, 1, 2],
                20f);
            return true;
        }

        private static float GetAdvance(string text)
        {
            return text == "\u0915\u093fB" ? 20f : text.Length * 10f;
        }
    }

    private sealed class RectGlyphPathAssetLoader : ISvgAssetLoader, ISvgTextRunTypefaceResolver, ISvgTextGlyphRunResolver, ISvgTextGlyphRunPathResolver
    {
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream) => LoadTestImage(stream);

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        {
            var resolvedText = text ?? string.Empty;
            return
            [
                new TypefaceSpan(resolvedText, GetAdvance(resolvedText), _typeface)
            ];
        }

        public SKFontMetrics GetFontMetrics(SKPaint paint)
        {
            return new SKFontMetrics
            {
                Ascent = -8f,
                Descent = 2f,
                Top = -8f,
                Bottom = 2f
            };
        }

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        {
            var advance = GetAdvance(text ?? string.Empty);
            bounds = advance > 0f ? new SKRect(0f, -8f, advance, 2f) : SKRect.Empty;
            return advance;
        }

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var path = new SKPath();
            var currentX = x;
            for (var i = 0; i < text.Length; i++)
            {
                path.AddRect(SKRect.Create(currentX, y - 5f, 8f, 10f));
                currentX += 10f;
            }

            return path;
        }

        public SKTypeface? FindRunTypeface(string? text, SKPaint paintPreferredTypeface) => _typeface;

        public bool TryShapeGlyphRun(string? text, SKPaint paint, out ShapedGlyphRun shapedRun)
        {
            if (string.IsNullOrEmpty(text))
            {
                shapedRun = default;
                return false;
            }

            var glyphs = new ushort[text.Length];
            var points = new SKPoint[text.Length];
            var clusters = new int[text.Length];
            var currentX = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                glyphs[i] = (ushort)(i + 1);
                points[i] = new SKPoint(currentX, 0f);
                clusters[i] = i;
                currentX += 10f;
            }

            shapedRun = new ShapedGlyphRun(glyphs, points, clusters, currentX);
            return true;
        }

        public bool TryGetGlyphRunPath(ShapedGlyphRun shapedRun, SKPaint paint, float x, float y, out SKPath path)
        {
            path = new SKPath();
            if (shapedRun.Points.Length != shapedRun.Glyphs.Length)
            {
                return false;
            }

            for (var i = 0; i < shapedRun.Points.Length; i++)
            {
                var point = shapedRun.Points[i];
                path.AddRect(SKRect.Create(x + point.X, y - 5f + point.Y, 8f, 10f));
            }

            return !path.IsEmpty;
        }

        private static float GetAdvance(string text)
        {
            return text.Length * 10f;
        }
    }

    private sealed class PathlessColorClusterAssetLoader : ISvgAssetLoader, ISvgTextRunTypefaceResolver, ISvgTextGlyphRunResolver
    {
        private const string ColorCluster = "\U0001F469\u200D\U0001F467";

        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream) => LoadTestImage(stream);

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        {
            var resolvedText = text ?? string.Empty;
            return
            [
                new TypefaceSpan(resolvedText, GetAdvance(resolvedText), _typeface)
            ];
        }

        public SKFontMetrics GetFontMetrics(SKPaint paint)
        {
            return new SKFontMetrics
            {
                Ascent = -8f,
                Descent = 2f,
                Top = -8f,
                Bottom = 2f
            };
        }

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        {
            var advance = GetAdvance(text ?? string.Empty);
            bounds = advance > 0f ? new SKRect(0f, -8f, advance, 2f) : SKRect.Empty;
            return advance;
        }

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y)
        {
            if (string.IsNullOrEmpty(text) || text.Contains(ColorCluster, StringComparison.Ordinal))
            {
                return null;
            }

            var path = new SKPath();
            var currentX = x;
            for (var i = 0; i < text.Length; i++)
            {
                path.AddRect(SKRect.Create(currentX, y - 5f, 8f, 10f));
                currentX += 10f;
            }

            return path;
        }

        public SKTypeface? FindRunTypeface(string? text, SKPaint paintPreferredTypeface) => _typeface;

        public bool TryShapeGlyphRun(string? text, SKPaint paint, out ShapedGlyphRun shapedRun)
        {
            if (text != $"A{ColorCluster}B")
            {
                shapedRun = default;
                return false;
            }

            shapedRun = new ShapedGlyphRun(
                [1, 2, 3],
                [new SKPoint(0f, 0f), new SKPoint(10f, 0f), new SKPoint(30f, 0f)],
                [0, 1, 6],
                40f);
            return true;
        }

        private static float GetAdvance(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            var advance = 0f;
            for (var i = 0; i < text.Length;)
            {
                if (i + ColorCluster.Length <= text.Length &&
                    string.CompareOrdinal(text, i, ColorCluster, 0, ColorCluster.Length) == 0)
                {
                    advance += 20f;
                    i += ColorCluster.Length;
                    continue;
                }

                advance += 10f;
                i += char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                    ? 2
                    : 1;
            }

            return advance;
        }
    }

    private sealed class ShapedClusterAdvanceAssetLoader : ISvgAssetLoader, ISvgTextRunTypefaceResolver, ISvgTextGlyphClusterResolver
    {
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream) => LoadTestImage(stream);

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        {
            var resolvedText = text ?? string.Empty;
            return
            [
                new TypefaceSpan(resolvedText, GetAdvance(resolvedText), _typeface)
            ];
        }

        public SKFontMetrics GetFontMetrics(SKPaint paint)
        {
            var size = paint.TextSize > 0f ? paint.TextSize : 10f;
            return new SKFontMetrics
            {
                Ascent = -size * 0.8f,
                Descent = size * 0.2f,
                Top = -size * 0.8f,
                Bottom = size * 0.2f,
                Leading = 0f
            };
        }

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        {
            var advance = GetAdvance(text ?? string.Empty);
            var metrics = GetFontMetrics(paint);
            bounds = advance > 0f
                ? new SKRect(0f, metrics.Ascent, advance, metrics.Descent)
                : SKRect.Empty;
            return advance;
        }

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y) => null;

        public SKTypeface? FindRunTypeface(string? text, SKPaint paintPreferredTypeface) => _typeface;

        public bool TryShapeGlyphClusters(string? text, SKPaint paint, bool rightToLeft, out ShapedGlyphRun shapedRun, out ShapedTextCluster[] clusters)
        {
            if (text != "fiX")
            {
                shapedRun = default;
                clusters = Array.Empty<ShapedTextCluster>();
                return false;
            }

            shapedRun = new ShapedGlyphRun(
                [1, 2],
                [new SKPoint(0f, 0f), new SKPoint(10f, 0f)],
                [0, 2],
                20f);
            clusters =
            [
                new ShapedTextCluster(0, 2, 0, 1, 0f, 10f),
                new ShapedTextCluster(2, 1, 1, 1, 10f, 10f)
            ];
            return true;
        }

        private static float GetAdvance(string text)
        {
            var advance = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                advance += 8f;
            }

            return advance;
        }
    }

    private sealed class CountingNaturalAdvanceAssetLoader : ISvgAssetLoader
    {
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public bool EnableSvgFonts => false;

        public int FindTypefacesCallCount { get; private set; }

        public SKImage LoadImage(Stream stream) => LoadTestImage(stream);

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        {
            FindTypefacesCallCount++;
            var resolvedText = text ?? string.Empty;
            return
            [
                new TypefaceSpan(resolvedText, GetAdvance(resolvedText, paintPreferredTypeface), _typeface)
            ];
        }

        public SKFontMetrics GetFontMetrics(SKPaint paint)
        {
            var size = paint.TextSize > 0f ? paint.TextSize : 10f;
            return new SKFontMetrics
            {
                Ascent = -size * 0.8f,
                Descent = size * 0.2f,
                Top = -size * 0.8f,
                Bottom = size * 0.2f,
                Leading = 0f
            };
        }

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        {
            var advance = GetAdvance(text ?? string.Empty, paint);
            var metrics = GetFontMetrics(paint);
            bounds = advance > 0f
                ? new SKRect(0f, metrics.Ascent, advance, metrics.Descent)
                : SKRect.Empty;
            return advance;
        }

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y) => null;

        private static float GetAdvance(string text, SKPaint paint)
        {
            var size = paint.TextSize > 0f ? paint.TextSize : 10f;
            return text.Length * size;
        }
    }

    private sealed class FixedAdvanceAssetLoader : ISvgAssetLoader, ISvgImageAlphaProvider, ISvgTextRunTypefaceResolver, ISvgTextGlyphRunResolver
    {
        private readonly float _codepointAdvance;
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public FixedAdvanceAssetLoader(float codepointAdvance)
        {
            _codepointAdvance = codepointAdvance;
        }

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream) => LoadTestImage(stream);

        public bool TryGetImageAlpha(SKImage image, out int width, out int height, out byte[] alpha)
        {
            return TryDecodeNativeImageAlpha(image, out width, out height, out alpha);
        }

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        {
            var resolvedText = text ?? string.Empty;
            return
            [
                new TypefaceSpan(resolvedText, GetAdvance(resolvedText), _typeface)
            ];
        }

        public SKFontMetrics GetFontMetrics(SKPaint paint)
        {
            var size = paint.TextSize > 0f ? paint.TextSize : 10f;
            return new SKFontMetrics
            {
                Ascent = -size * 0.8f,
                Descent = size * 0.2f,
                Top = -size * 0.8f,
                Bottom = size * 0.2f,
                Leading = 0f
            };
        }

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        {
            var advance = GetAdvance(text ?? string.Empty);
            var metrics = GetFontMetrics(paint);
            bounds = advance > 0f
                ? new SKRect(0f, metrics.Ascent, advance, metrics.Descent)
                : SKRect.Empty;
            return advance;
        }

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y) => null;

        public SKTypeface? FindRunTypeface(string? text, SKPaint paintPreferredTypeface) => _typeface;

        public bool TryShapeGlyphRun(string? text, SKPaint paint, out ShapedGlyphRun shapedRun)
        {
            if (string.IsNullOrEmpty(text))
            {
                shapedRun = default;
                return false;
            }

            var glyphs = new ushort[text.Length];
            var points = new SKPoint[text.Length];
            var clusters = new int[text.Length];
            var currentX = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                glyphs[i] = (ushort)(i + 1);
                points[i] = new SKPoint(currentX, 0f);
                clusters[i] = i;
                currentX += GetAdvance(text[i].ToString());
            }

            shapedRun = new ShapedGlyphRun(glyphs, points, clusters, currentX);
            return true;
        }

        private float GetAdvance(string text)
        {
            var advance = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                if (!IsZeroAdvanceControl(text[i]))
                {
                    advance += _codepointAdvance;
                }
            }

            return advance;
        }

        private static bool IsZeroAdvanceControl(char value)
        {
            return value is '\r' or '\n' or '\u034F' or '\u061C' or '\u200C' or '\u200D' or
                '\u200E' or '\u200F' or '\u202A' or '\u202B' or '\u202C' or '\u202D' or
                '\u202E' or '\u2060' or '\u2066' or '\u2067' or '\u2068' or '\u2069' or '\uFEFF';
        }

    }
}
