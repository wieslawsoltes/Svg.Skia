using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia.Benchmarks;

public class SvgRetainedSceneMutationBenchmarks
{
    private SvgLoadPipelineBenchmarkScenario? _scenario;
    private SvgSceneDocument? _sceneDocument;
    private SvgVisualElement? _mutationTarget;
    private SKSvg? _svg;
    private SvgVisualElement? _renderMutationTarget;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
    }

    [IterationSetup(Target = nameof(ApplyFillMutationOnly))]
    public void SetupApplyFillMutation()
    {
        var scenario = _scenario ?? throw new InvalidOperationException("Benchmark scenario is not initialized.");
        var parsedDocument = SvgBenchmarkHelpers.ParseDocument(scenario);
        var skiaModel = new SkiaModel(new SKSvgSettings());
        var assetLoader = new SkiaSvgAssetLoader(skiaModel);
        if (!SvgSceneRuntime.TryCompile(parsedDocument, assetLoader, DrawAttributes.None, out var sceneDocument) ||
            sceneDocument is null)
        {
            throw new InvalidOperationException($"Failed to compile retained scene for '{ScenarioName}'.");
        }

        _sceneDocument = sceneDocument;
        _mutationTarget = parsedDocument.Descendants().OfType<SvgVisualElement>().First(static visual => visual.Fill is not null);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mutation", "Compile")]
    public int ApplyFillMutationOnly()
    {
        _mutationTarget!.Fill = new SvgColourServer(Color.BlueViolet);
        var result = _sceneDocument!.ApplyMutation(_mutationTarget, new[] { "fill" });
        return result.Succeeded ? result.CompilationRootCount : -1;
    }

    [IterationSetup(Target = nameof(ApplyFillMutationAndRender))]
    public void SetupApplyFillMutationAndRender()
    {
        InitializeRenderMutationScenario();
    }

    [IterationSetup(Target = nameof(ApplyFillMutationAndFullRebuild))]
    public void SetupApplyFillMutationAndFullRebuild()
    {
        InitializeRenderMutationScenario();
    }

    [IterationCleanup(Target = nameof(ApplyFillMutationAndRender))]
    public void CleanupApplyFillMutationAndRender()
    {
        DisposeRenderMutationScenario();
    }

    [IterationCleanup(Target = nameof(ApplyFillMutationAndFullRebuild))]
    public void CleanupApplyFillMutationAndFullRebuild()
    {
        DisposeRenderMutationScenario();
    }

    private void InitializeRenderMutationScenario()
    {
        var scenario = _scenario ?? throw new InvalidOperationException("Benchmark scenario is not initialized.");
        _svg = new SKSvg();
        _svg.FromSvg(scenario.SvgText);
        _renderMutationTarget = _svg.SourceDocument?.Descendants().OfType<SvgVisualElement>().First(static visual => visual.Fill is not null)
            ?? throw new InvalidOperationException($"Failed to locate a fill target for '{ScenarioName}'.");
    }

    private void DisposeRenderMutationScenario()
    {
        _svg?.Dispose();
        _svg = null;
        _renderMutationTarget = null;
    }

    [Benchmark]
    [BenchmarkCategory("Mutation", "Render")]
    public int ApplyFillMutationAndRender()
    {
        _renderMutationTarget!.Fill = new SvgColourServer(Color.BlueViolet);
        return _svg!.TryApplyRetainedSceneMutationAndRender(_renderMutationTarget, new[] { "fill" }, out var result) &&
               result is not null
            ? result.CompilationRootCount
            : -1;
    }

    [Benchmark]
    [BenchmarkCategory("Mutation", "FullRebuild")]
    public float ApplyFillMutationAndFullRebuild()
    {
        _renderMutationTarget!.Fill = new SvgColourServer(Color.BlueViolet);
        using var picture = _svg!.FromSvgDocument(_svg.SourceDocument);
        return picture?.CullRect.Width ?? 0f;
    }
}
