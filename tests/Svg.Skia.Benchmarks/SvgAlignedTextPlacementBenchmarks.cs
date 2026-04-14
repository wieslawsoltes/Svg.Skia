using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Svg.Model;

namespace Svg.Skia.Benchmarks;

public class SvgAlignedTextPlacementBenchmarks
{
    private SkiaSvgAssetLoader? assetLoader;
    private SvgSceneTextCompiler.AlignedCodepointPlacementBenchmarkInput[] inputs = Array.Empty<SvgSceneTextCompiler.AlignedCodepointPlacementBenchmarkInput>();

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names
        .Where(static name => name.Contains("aligned", StringComparison.OrdinalIgnoreCase));

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        var document = SvgBenchmarkHelpers.ParseDocument(scenario);
        var geometryBounds = SvgBenchmarkHelpers.GetDocumentViewport(document);
        assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        inputs = document.Descendants()
            .OfType<SvgTextBase>()
            .Where(static textBase => textBase is not SvgTextPath && !string.IsNullOrWhiteSpace(textBase.Content))
            .Select(textBase => SvgSceneTextCompiler.CreateAlignedCodepointPlacementBenchmarkInput(textBase, textBase.Content!, geometryBounds))
            .ToArray();

        if (inputs.Length == 0)
        {
            throw new InvalidOperationException($"Scenario '{ScenarioName}' did not contain any aligned text fragments.");
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Compile", "Text", "Placements", "Aligned")]
    public int CreateAlignedPlacementsAcrossFragments()
    {
        var totalPlacements = 0;
        for (var i = 0; i < inputs.Length; i++)
        {
            if (SvgSceneTextCompiler.TryBenchmarkAlignedCodepointPlacements(inputs[i], assetLoader!, out var placementCount, out _))
            {
                totalPlacements += placementCount;
            }
        }

        return totalPlacements;
    }
}
