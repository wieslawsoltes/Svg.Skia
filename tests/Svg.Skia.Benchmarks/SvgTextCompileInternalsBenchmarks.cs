using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using ShimSkiaSharp;
using Svg.Model;

namespace Svg.Skia.Benchmarks;

public class SvgTextCompileInternalsBenchmarks
{
    private delegate List<string> SplitCodepointsDelegate(string text);
    private delegate float MeasureNaturalTextAdvanceDelegate(SvgTextBase svgTextBase, string text, SKRect geometryBounds, ISvgAssetLoader assetLoader);
    private delegate float[] MeasureNaturalCodepointAdvancesDelegate(SvgTextBase svgTextBase, IReadOnlyList<string> codepoints, SKRect geometryBounds, ISvgAssetLoader assetLoader);
    private delegate object MeasureLineStatsDelegate(SvgTextBase svgTextBase, string text, SKRect geometryBounds, ISvgAssetLoader assetLoader);
    private delegate SKPaint CreateTextMetricsPaintDelegate(SvgTextBase svgTextBase, SKRect geometryBounds);
    private delegate IDisposable BeginCompileLineStatsCacheScopeDelegate();

    private sealed record TextFragment(SvgTextBase StyleSource, string Text);

    private static readonly SplitCodepointsDelegate s_splitCodepoints =
        CreateDelegate<SplitCodepointsDelegate>("SplitCodepoints");

    private static readonly MeasureNaturalTextAdvanceDelegate s_measureNaturalTextAdvance =
        CreateDelegate<MeasureNaturalTextAdvanceDelegate>("MeasureNaturalTextAdvance");

    private static readonly MeasureNaturalCodepointAdvancesDelegate s_measureNaturalCodepointAdvances =
        CreateDelegate<MeasureNaturalCodepointAdvancesDelegate>("MeasureNaturalCodepointAdvances");

    private static readonly MeasureLineStatsDelegate s_measureLineStats =
        CreateDelegate<MeasureLineStatsDelegate>("MeasureLineStats");

    private static readonly CreateTextMetricsPaintDelegate s_createTextMetricsPaint =
        CreateDelegate<CreateTextMetricsPaintDelegate>("CreateTextMetricsPaint");

    private static readonly BeginCompileLineStatsCacheScopeDelegate s_beginCompileLineStatsCacheScope =
        CreateDelegate<BeginCompileLineStatsCacheScopeDelegate>("BeginCompileLineStatsCacheScope");

    private SkiaSvgAssetLoader? assetLoader;
    private SKRect geometryBounds;
    private TextFragment[] textFragments = Array.Empty<TextFragment>();
    private IReadOnlyList<string>[] splitCodepoints = Array.Empty<IReadOnlyList<string>>();
    private IDisposable? compileLineStatsCacheScope;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names
        .Where(static name =>
            name.Contains("text", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("file:", StringComparison.OrdinalIgnoreCase));

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        var document = SvgBenchmarkHelpers.ParseDocument(scenario);
        geometryBounds = SvgBenchmarkHelpers.GetDocumentViewport(document);
        assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var fragments = new List<TextFragment>();
        foreach (var text in document.Descendants().OfType<SvgText>())
        {
            if (!string.IsNullOrWhiteSpace(text.Content))
            {
                fragments.Add(new TextFragment(text, text.Content));
            }
        }

        foreach (var span in document.Descendants().OfType<SvgTextSpan>())
        {
            if (!string.IsNullOrWhiteSpace(span.Content))
            {
                fragments.Add(new TextFragment(span, span.Content));
            }
        }

        if (fragments.Count == 0)
        {
            throw new InvalidOperationException($"Scenario '{ScenarioName}' did not contain any text fragments.");
        }

        textFragments = fragments.ToArray();
        splitCodepoints = textFragments
            .Select(static fragment => (IReadOnlyList<string>)s_splitCodepoints(fragment.Text))
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Compile", "Text", "Internals", "SplitCodepoints")]
    public int SplitCodepointsAcrossFragments()
    {
        var totalCount = 0;
        for (var i = 0; i < textFragments.Length; i++)
        {
            totalCount += s_splitCodepoints(textFragments[i].Text).Count;
        }

        return totalCount;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "CreateTextMetricsPaint")]
    public float CreateTextMetricsPaintAcrossFragments()
    {
        var totalSize = 0f;
        for (var i = 0; i < textFragments.Length; i++)
        {
            totalSize += s_createTextMetricsPaint(textFragments[i].StyleSource, geometryBounds).TextSize;
        }

        return totalSize;
    }

    [IterationSetup(Target = nameof(CreateTextMetricsPaintAcrossFragmentsHotWithinCompileScope))]
    public void SetupCreateTextMetricsPaintAcrossFragmentsHotWithinCompileScope()
    {
        compileLineStatsCacheScope = s_beginCompileLineStatsCacheScope();
        for (var i = 0; i < textFragments.Length; i++)
        {
            _ = s_createTextMetricsPaint(textFragments[i].StyleSource, geometryBounds);
        }
    }

    [IterationCleanup(Target = nameof(CreateTextMetricsPaintAcrossFragmentsHotWithinCompileScope))]
    public void CleanupCreateTextMetricsPaintAcrossFragmentsHotWithinCompileScope()
    {
        compileLineStatsCacheScope?.Dispose();
        compileLineStatsCacheScope = null;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "CreateTextMetricsPaint", "CompileCache")]
    public float CreateTextMetricsPaintAcrossFragmentsHotWithinCompileScope()
    {
        var totalSize = 0f;
        for (var i = 0; i < textFragments.Length; i++)
        {
            totalSize += s_createTextMetricsPaint(textFragments[i].StyleSource, geometryBounds).TextSize;
        }

        return totalSize;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "MeasureNaturalTextAdvance")]
    public float MeasureNaturalTextAdvanceAcrossFragments()
    {
        var totalAdvance = 0f;
        for (var i = 0; i < textFragments.Length; i++)
        {
            var fragment = textFragments[i];
            totalAdvance += s_measureNaturalTextAdvance(fragment.StyleSource, fragment.Text, geometryBounds, assetLoader!);
        }

        return totalAdvance;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "MeasureNaturalTextAdvance", "FreshAssetLoader")]
    public float MeasureNaturalTextAdvanceAcrossFragmentsWithFreshAssetLoader()
    {
        var freshAssetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var totalAdvance = 0f;
        for (var i = 0; i < textFragments.Length; i++)
        {
            var fragment = textFragments[i];
            totalAdvance += s_measureNaturalTextAdvance(fragment.StyleSource, fragment.Text, geometryBounds, freshAssetLoader);
        }

        return totalAdvance;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "MeasureLineStats")]
    public int MeasureLineStatsAcrossFragments()
    {
        var totalCount = 0;
        for (var i = 0; i < textFragments.Length; i++)
        {
            var fragment = textFragments[i];
            if (s_measureLineStats(fragment.StyleSource, fragment.Text, geometryBounds, assetLoader!) is not null)
            {
                totalCount++;
            }
        }

        return totalCount;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "MeasureLineStats", "FreshAssetLoader")]
    public int MeasureLineStatsAcrossFragmentsWithFreshAssetLoader()
    {
        var freshAssetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var totalCount = 0;
        for (var i = 0; i < textFragments.Length; i++)
        {
            var fragment = textFragments[i];
            if (s_measureLineStats(fragment.StyleSource, fragment.Text, geometryBounds, freshAssetLoader) is not null)
            {
                totalCount++;
            }
        }

        return totalCount;
    }

    [IterationSetup(Target = nameof(MeasureLineStatsAcrossFragmentsHotWithinCompileScope))]
    public void SetupMeasureLineStatsAcrossFragmentsHotWithinCompileScope()
    {
        compileLineStatsCacheScope = s_beginCompileLineStatsCacheScope();
        for (var i = 0; i < textFragments.Length; i++)
        {
            var fragment = textFragments[i];
            _ = s_measureLineStats(fragment.StyleSource, fragment.Text, geometryBounds, assetLoader!);
        }
    }

    [IterationCleanup(Target = nameof(MeasureLineStatsAcrossFragmentsHotWithinCompileScope))]
    public void CleanupMeasureLineStatsAcrossFragmentsHotWithinCompileScope()
    {
        compileLineStatsCacheScope?.Dispose();
        compileLineStatsCacheScope = null;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "MeasureLineStats", "CompileCache")]
    public int MeasureLineStatsAcrossFragmentsHotWithinCompileScope()
    {
        var totalCount = 0;
        for (var i = 0; i < textFragments.Length; i++)
        {
            var fragment = textFragments[i];
            if (s_measureLineStats(fragment.StyleSource, fragment.Text, geometryBounds, assetLoader!) is not null)
            {
                totalCount++;
            }
        }

        return totalCount;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "MeasureNaturalCodepointAdvances")]
    public int MeasureNaturalCodepointAdvancesAcrossFragments()
    {
        var totalCount = 0;
        for (var i = 0; i < textFragments.Length; i++)
        {
            totalCount += s_measureNaturalCodepointAdvances(
                textFragments[i].StyleSource,
                splitCodepoints[i],
                geometryBounds,
                assetLoader!).Length;
        }

        return totalCount;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "MeasureNaturalCodepointAdvances", "FreshAssetLoader")]
    public int MeasureNaturalCodepointAdvancesAcrossFragmentsWithFreshAssetLoader()
    {
        var freshAssetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var totalCount = 0;
        for (var i = 0; i < textFragments.Length; i++)
        {
            totalCount += s_measureNaturalCodepointAdvances(
                textFragments[i].StyleSource,
                splitCodepoints[i],
                geometryBounds,
                freshAssetLoader).Length;
        }

        return totalCount;
    }

    private static TDelegate CreateDelegate<TDelegate>(string methodName)
        where TDelegate : Delegate
    {
        var method = typeof(SvgSceneTextCompiler).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        if (method is null)
        {
            throw new InvalidOperationException($"Could not locate SvgSceneTextCompiler.{methodName}.");
        }

        return method.CreateDelegate<TDelegate>();
    }
}
