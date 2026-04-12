using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Svg.Model;

namespace Svg.Skia.Benchmarks;

public class SvgShimPictureModelBenchmarks
{
    private SvgSceneDocument? sceneDocument;
    private SvgSceneNode? topLevelNode;
    private SvgSceneNode? leafNode;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        var parsedDocument = SvgBenchmarkHelpers.ParseDocument(scenario);
        var skiaModel = new SkiaModel(new SKSvgSettings());
        var assetLoader = new SkiaSvgAssetLoader(skiaModel);

        if (!SvgSceneRuntime.TryCompile(parsedDocument, assetLoader, DrawAttributes.None, out sceneDocument) ||
            sceneDocument is null)
        {
            throw new InvalidOperationException($"Failed to compile retained scene for benchmark scenario '{ScenarioName}'.");
        }

        topLevelNode = SvgBenchmarkHelpers.GetTopLevelNode(sceneDocument);
        leafNode = SvgBenchmarkHelpers.GetLeafNode(sceneDocument);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ShimModel", "Full")]
    public int CreateFullModel()
    {
        var model = sceneDocument!.CreateModel();
        return model?.Commands?.Count ?? -1;
    }

    [Benchmark]
    [BenchmarkCategory("ShimModel", "TopLevelNode")]
    public int CreateTopLevelNodeModel()
    {
        var model = sceneDocument!.CreateNodeModel(topLevelNode!);
        return model?.Commands?.Count ?? -1;
    }

    [Benchmark]
    [BenchmarkCategory("ShimModel", "LeafNode")]
    public int CreateLeafNodeModel()
    {
        var model = sceneDocument!.CreateNodeModel(leafNode!);
        return model?.Commands?.Count ?? -1;
    }
}
