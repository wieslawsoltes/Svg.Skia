using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Svg;

namespace Svg.Skia.Benchmarks;

public class SvgCustomParsePhaseBenchmarks
{
    private string svgText = string.Empty;
    private Uri? baseUri;
    private SvgCompatibilityLoadResult? preparedLoadResult;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        svgText = scenario.SvgText;
        baseUri = scenario.BaseUri;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Parse", "Structure")]
    public int LoadStructureOnly()
    {
        var loadResult = SvgDocumentCompatibilityLoader.LoadStructure<SvgDocument>(svgText, baseUri: baseUri);
        return loadResult.Document.Children.Count;
    }

    [IterationSetup(Target = nameof(ApplyCssCompatibilityOnly))]
    public void SetupApplyCssCompatibility()
    {
        preparedLoadResult = SvgDocumentCompatibilityLoader.LoadStructure<SvgDocument>(svgText, baseUri: baseUri);
    }

    [Benchmark]
    [BenchmarkCategory("Parse", "Css")]
    public int ApplyCssCompatibilityOnly()
    {
        SvgDocumentCompatibilityLoader.ApplyCompatibilityCss(preparedLoadResult!);
        return preparedLoadResult!.Document.Children.Count;
    }

    [IterationSetup(Target = nameof(FlushStylesOnlyAfterStructureBuild))]
    public void SetupFlushStyles()
    {
        preparedLoadResult = SvgDocumentCompatibilityLoader.LoadStructure<SvgDocument>(svgText, baseUri: baseUri);
    }

    [Benchmark]
    [BenchmarkCategory("Parse", "Flush")]
    public int FlushStylesOnlyAfterStructureBuild()
    {
        SvgDocumentCompatibilityLoader.FlushCompatibilityStyles(preparedLoadResult!);
        return preparedLoadResult!.Document.Children.Count;
    }

    [IterationSetup(Target = nameof(FinalizeAfterStructureBuild))]
    public void SetupFinalize()
    {
        preparedLoadResult = SvgDocumentCompatibilityLoader.LoadStructure<SvgDocument>(svgText, baseUri: baseUri);
    }

    [Benchmark]
    [BenchmarkCategory("Parse", "Finalize")]
    public int FinalizeAfterStructureBuild()
    {
        SvgDocumentCompatibilityLoader.FinalizeDocument(preparedLoadResult!);
        return preparedLoadResult!.Document.Children.Count;
    }
}
