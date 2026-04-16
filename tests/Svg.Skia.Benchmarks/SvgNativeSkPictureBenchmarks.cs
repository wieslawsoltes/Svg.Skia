using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using ShimSkiaSharp;
using Svg;
using Svg.Model;

namespace Svg.Skia.Benchmarks;

public class SvgNativeSkPictureBenchmarks
{
    private SkiaModel? skiaModel;
    private SKSvg? retainedSvg;
    private SvgSceneDocument? sceneDocument;
    private SKPicture? fullModel;
    private SKPicture? topLevelNodeModel;
    private SKPicture? leafNodeModel;
    private SKPicture? emptyModel;
    private SvgSceneNode? retainedTopLevelNode;
    private SvgSceneNode? retainedLeafNode;
    private SKRect retainedTopLevelNodeClip;
    private SKRect retainedLeafNodeClip;
    private SkiaSharp.SKRect fullModelCullRect;
    private SkiaSharp.SKPicture? cachedRetainedPicture;
    private SkiaSharp.SKPicture? cachedRetainedTopLevelNodePicture;
    private SkiaSharp.SKPicture? cachedRetainedLeafNodePicture;
    private SkiaSharp.SKPicture? cachedClippedRetainedTopLevelNodePicture;
    private SkiaSharp.SKPicture? cachedClippedRetainedLeafNodePicture;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        var parsedDocument = SvgBenchmarkHelpers.ParseDocument(scenario);
        skiaModel = new SkiaModel(new SKSvgSettings());
        var assetLoader = new SkiaSvgAssetLoader(skiaModel);
        retainedSvg = new SKSvg();
        retainedSvg.FromSvgDocument((SvgDocument)parsedDocument.DeepCopy());

        if (!SvgSceneRuntime.TryCompile(parsedDocument, assetLoader, DrawAttributes.None, out sceneDocument) ||
            sceneDocument is null)
        {
            throw new InvalidOperationException($"Failed to compile retained scene for benchmark scenario '{ScenarioName}'.");
        }

        var retainedSceneDocument = retainedSvg.RetainedSceneGraph
            ?? throw new InvalidOperationException($"Failed to build retained scene graph for benchmark scenario '{ScenarioName}'.");

        fullModel = sceneDocument.CreateModel() ?? throw new InvalidOperationException($"Failed to create full model for benchmark scenario '{ScenarioName}'.");
        topLevelNodeModel = sceneDocument.CreateNodeModel(SvgBenchmarkHelpers.GetTopLevelNode(sceneDocument))
            ?? throw new InvalidOperationException($"Failed to create top-level node model for benchmark scenario '{ScenarioName}'.");
        leafNodeModel = sceneDocument.CreateNodeModel(SvgBenchmarkHelpers.GetLeafNode(sceneDocument))
            ?? throw new InvalidOperationException($"Failed to create leaf node model for benchmark scenario '{ScenarioName}'.");
        emptyModel = new SKPicture(fullModel.CullRect, new List<CanvasCommand>());
        fullModelCullRect = skiaModel.ToSKRect(fullModel.CullRect);
        retainedTopLevelNode = SvgBenchmarkHelpers.GetTopLevelNode(retainedSceneDocument);
        retainedLeafNode = SvgBenchmarkHelpers.GetLeafNode(retainedSceneDocument);
        retainedTopLevelNodeClip = retainedTopLevelNode.TransformedBounds;
        retainedLeafNodeClip = retainedLeafNode.TransformedBounds;
        cachedRetainedPicture = retainedSvg.RetainedPicture
            ?? throw new InvalidOperationException($"Failed to warm retained native picture cache for benchmark scenario '{ScenarioName}'.");
        cachedRetainedTopLevelNodePicture = retainedSvg.GetCachedRetainedSceneNodePicture(retainedTopLevelNode)
            ?? throw new InvalidOperationException($"Failed to warm retained top-level node picture cache for benchmark scenario '{ScenarioName}'.");
        cachedRetainedLeafNodePicture = retainedSvg.GetCachedRetainedSceneNodePicture(retainedLeafNode)
            ?? throw new InvalidOperationException($"Failed to warm retained leaf node picture cache for benchmark scenario '{ScenarioName}'.");
        cachedClippedRetainedTopLevelNodePicture = retainedSvg.GetCachedRetainedSceneNodePicture(retainedTopLevelNode, retainedTopLevelNodeClip)
            ?? throw new InvalidOperationException($"Failed to warm clipped retained top-level node picture cache for benchmark scenario '{ScenarioName}'.");
        cachedClippedRetainedLeafNodePicture = retainedSvg.GetCachedRetainedSceneNodePicture(retainedLeafNode, retainedLeafNodeClip)
            ?? throw new InvalidOperationException($"Failed to warm clipped retained leaf node picture cache for benchmark scenario '{ScenarioName}'.");
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NativePicture", "Full")]
    public float CreateNativePictureFromFullModel()
    {
        using var picture = skiaModel!.ToSKPicture(fullModel!);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "Full", "DirectSceneDocument")]
    public float CreateNativePictureDirectFromRetainedSceneGraph()
    {
        using var picture = retainedSvg!.CreateRetainedSceneGraphPicture();
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "Full", "FreshSkiaModel")]
    public float CreateNativePictureFromFullModelWithFreshSkiaModel()
    {
        var localSkiaModel = new SkiaModel(new SKSvgSettings());
        using var picture = localSkiaModel.ToSKPicture(fullModel!);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "Full", "CachedRetainedPicture")]
    public float GetCachedRetainedPicture()
    {
        return retainedSvg!.RetainedPicture?.CullRect.Width ?? cachedRetainedPicture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "Full", "ShimThenNative")]
    public float CreateNativePictureFromRetainedSceneGraphViaShimModel()
    {
        var model = sceneDocument!.CreateModel();
        using var picture = model is null ? null : retainedSvg!.SkiaModel.ToSKPicture(model);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "TopLevelNode")]
    public float CreateNativePictureFromTopLevelNodeModel()
    {
        using var picture = skiaModel!.ToSKPicture(topLevelNodeModel!);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "TopLevelNode", "DirectSceneDocument")]
    public float CreateNativePictureDirectFromTopLevelRetainedNode()
    {
        using var picture = retainedSvg!.CreateRetainedSceneNodePicture(retainedTopLevelNode!);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "TopLevelNode", "CachedRetainedPicture")]
    public float GetCachedTopLevelRetainedSceneNodePicture()
    {
        return retainedSvg!.GetCachedRetainedSceneNodePicture(retainedTopLevelNode!)?.CullRect.Width
               ?? cachedRetainedTopLevelNodePicture?.CullRect.Width
               ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "TopLevelNode", "Clipped", "DirectSceneDocument")]
    public float CreateNativePictureDirectFromTopLevelRetainedNodeClipped()
    {
        using var picture = retainedSvg!.CreateRetainedSceneNodePicture(retainedTopLevelNode!, retainedTopLevelNodeClip);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "TopLevelNode", "Clipped", "CachedRetainedPicture")]
    public float GetCachedTopLevelClippedRetainedSceneNodePicture()
    {
        return retainedSvg!.GetCachedRetainedSceneNodePicture(retainedTopLevelNode!, retainedTopLevelNodeClip)?.CullRect.Width
               ?? cachedClippedRetainedTopLevelNodePicture?.CullRect.Width
               ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "LeafNode")]
    public float CreateNativePictureFromLeafNodeModel()
    {
        using var picture = skiaModel!.ToSKPicture(leafNodeModel!);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "LeafNode", "DirectSceneDocument")]
    public float CreateNativePictureDirectFromLeafRetainedNode()
    {
        using var picture = retainedSvg!.CreateRetainedSceneNodePicture(retainedLeafNode!);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "LeafNode", "CachedRetainedPicture")]
    public float GetCachedLeafRetainedSceneNodePicture()
    {
        return retainedSvg!.GetCachedRetainedSceneNodePicture(retainedLeafNode!)?.CullRect.Width
               ?? cachedRetainedLeafNodePicture?.CullRect.Width
               ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "LeafNode", "Clipped", "DirectSceneDocument")]
    public float CreateNativePictureDirectFromLeafRetainedNodeClipped()
    {
        using var picture = retainedSvg!.CreateRetainedSceneNodePicture(retainedLeafNode!, retainedLeafNodeClip);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "LeafNode", "Clipped", "CachedRetainedPicture")]
    public float GetCachedLeafClippedRetainedSceneNodePicture()
    {
        return retainedSvg!.GetCachedRetainedSceneNodePicture(retainedLeafNode!, retainedLeafNodeClip)?.CullRect.Width
               ?? cachedClippedRetainedLeafNodePicture?.CullRect.Width
               ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "Empty")]
    public float CreateNativePictureFromEmptyModel()
    {
        using var picture = skiaModel!.ToSKPicture(emptyModel!);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "Replay", "ForLoop")]
    public float ReplayFullModelIntoRecorderCanvasUsingCurrentLoop()
    {
        using var recorder = new SkiaSharp.SKPictureRecorder();
        using var canvas = recorder.BeginRecording(fullModelCullRect);
        skiaModel!.Draw(fullModel!, canvas);
        using var picture = recorder.EndRecording();
        return picture.CullRect.Width;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "Replay", "ForLoop", "PerCommandDispatch")]
    public float ReplayFullModelIntoRecorderCanvasUsingForLoopPerCommandDispatch()
    {
        using var recorder = new SkiaSharp.SKPictureRecorder();
        using var canvas = recorder.BeginRecording(fullModelCullRect);
        var commands = fullModel!.Commands;
        if (commands is { Count: > 0 })
        {
            for (var i = 0; i < commands.Count; i++)
            {
                skiaModel!.Draw(commands[i], canvas);
            }
        }

        using var picture = recorder.EndRecording();
        return picture.CullRect.Width;
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "Replay", "ForeachLoop")]
    public float ReplayFullModelIntoRecorderCanvasUsingForeachLoop()
    {
        using var recorder = new SkiaSharp.SKPictureRecorder();
        using var canvas = recorder.BeginRecording(fullModelCullRect);
        DrawWithForeach(fullModel!, canvas);
        using var picture = recorder.EndRecording();
        return picture.CullRect.Width;
    }

    private void DrawWithForeach(SKPicture picture, SkiaSharp.SKCanvas canvas)
    {
        var commands = picture.Commands;
        if (commands is null)
        {
            return;
        }

        foreach (var command in commands)
        {
            skiaModel!.Draw(command, canvas);
        }
    }
}
