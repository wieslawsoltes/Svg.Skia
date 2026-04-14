using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia.Benchmarks;

public class SvgRetainedSceneCompileBenchmarks
{
    private SvgDocument? parsedDocument;
    private SkiaSvgAssetLoader? assetLoader;
    private SkiaModel? skiaModel;
    private SvgSceneDocument? preparedSceneDocument;
    private SvgSceneNode? compiledRootNode;
    private SvgElementAddressKeyCache? phaseAddressKeyCache;
    private SKRect compiledCullRect;
    private SKRect compiledViewport;
    private SKRect viewport;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        parsedDocument = SvgBenchmarkHelpers.ParseDocument(scenario);
        viewport = SvgBenchmarkHelpers.GetDocumentViewport(parsedDocument);
        skiaModel = new SkiaModel(new SKSvgSettings());
        assetLoader = new SkiaSvgAssetLoader(skiaModel);

        if (!SvgSceneCompiler.TryCompileNodeTree(
                parsedDocument,
                viewport,
                assetLoader,
                DrawAttributes.None,
                out compiledRootNode,
                out compiledCullRect,
                out compiledViewport) ||
            compiledRootNode is null)
        {
            throw new InvalidOperationException($"Failed to compile benchmark scene tree for '{ScenarioName}'.");
        }

        preparedSceneDocument = new SvgSceneDocument(
            parsedDocument,
            compiledCullRect,
            compiledViewport,
            compiledRootNode,
            assetLoader,
            DrawAttributes.None);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Compile", "Runtime")]
    public int CompileViaSceneRuntime()
    {
        var succeeded = SvgSceneRuntime.TryCompile(parsedDocument!, assetLoader!, DrawAttributes.None, out var sceneDocument);
        return succeeded && sceneDocument is not null ? sceneDocument.Root.Children.Count : -1;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "DirectCompiler")]
    public int CompileViaSceneCompiler()
    {
        var succeeded = SvgSceneCompiler.TryCompile(parsedDocument!, viewport, assetLoader!, DrawAttributes.None, out var sceneDocument);
        return succeeded && sceneDocument is not null ? sceneDocument.Root.Children.Count : -1;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Tree")]
    public int CompileNodeTreeOnly()
    {
        var succeeded = SvgSceneCompiler.TryCompileNodeTree(
            parsedDocument!,
            viewport,
            assetLoader!,
            DrawAttributes.None,
            out var rootNode,
            out _,
            out _);
        return succeeded && rootNode is not null ? rootNode.Children.Count : -1;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "SceneDocument")]
    public int CreateSceneDocumentFromCompiledTree()
    {
        var sceneDocument = new SvgSceneDocument(
            parsedDocument!,
            compiledCullRect,
            compiledViewport,
            compiledRootNode!,
            assetLoader!,
            DrawAttributes.None);
        return sceneDocument.Root.Children.Count;
    }

    [IterationSetup(Target = nameof(ReindexSceneNodesOnly))]
    public void SetupReindexSceneNodes()
    {
        preparedSceneDocument!.ClearIndexesAndDependencies();
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Reindex")]
    public int ReindexSceneNodesOnly()
    {
        preparedSceneDocument!.ReindexNodes();
        return preparedSceneDocument.NodesById.Count;
    }

    [IterationSetup(Target = nameof(RebuildResourceGraphOnly))]
    public void SetupRebuildResourceGraph()
    {
        phaseAddressKeyCache = new SvgElementAddressKeyCache();
        preparedSceneDocument!.ClearIndexesAndDependencies();
        preparedSceneDocument.ReindexNodes();
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Resources")]
    public int RebuildResourceGraphOnly()
    {
        preparedSceneDocument!.RebuildResourceGraph(phaseAddressKeyCache!);
        return preparedSceneDocument.ResourcesById.Count;
    }

    [IterationSetup(Target = nameof(RegisterDependenciesOnly))]
    public void SetupRegisterDependencies()
    {
        phaseAddressKeyCache = new SvgElementAddressKeyCache();
        preparedSceneDocument!.ClearIndexesAndDependencies();
        preparedSceneDocument.ReindexNodes();
        preparedSceneDocument.RebuildResourceGraph(phaseAddressKeyCache);
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Dependencies")]
    public int RegisterDependenciesOnly()
    {
        preparedSceneDocument!.RegisterNodeDependencies(phaseAddressKeyCache!);
        return preparedSceneDocument.Revision > 0 ? 1 : 0;
    }

    [IterationSetup(Target = nameof(ResolveRuntimePayloadsOnly))]
    public void SetupResolveRuntimePayloads()
    {
        phaseAddressKeyCache = new SvgElementAddressKeyCache();
        preparedSceneDocument!.ClearIndexesAndDependencies();
        preparedSceneDocument.ReindexNodes();
        preparedSceneDocument.RebuildResourceGraph(phaseAddressKeyCache);
        preparedSceneDocument.RegisterNodeDependencies(phaseAddressKeyCache);
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "RuntimePayloads")]
    public int ResolveRuntimePayloadsOnly()
    {
        preparedSceneDocument!.ResolveRuntimePayloads(phaseAddressKeyCache!);
        return preparedSceneDocument.Root.Children.Count;
    }
}
