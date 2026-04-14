using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using ShimSkiaSharp;
using Svg.Model;

namespace Svg.Skia.Benchmarks;

public class SvgNativeSkPictureBenchmarks
{
    private SkiaModel? skiaModel;
    private SKPicture? fullModel;
    private SKPicture? topLevelNodeModel;
    private SKPicture? leafNodeModel;
    private SKPicture? emptyModel;
    private SkiaSharp.SKRect fullModelCullRect;

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

        if (!SvgSceneRuntime.TryCompile(parsedDocument, assetLoader, DrawAttributes.None, out var sceneDocument) ||
            sceneDocument is null)
        {
            throw new InvalidOperationException($"Failed to compile retained scene for benchmark scenario '{ScenarioName}'.");
        }

        fullModel = sceneDocument.CreateModel() ?? throw new InvalidOperationException($"Failed to create full model for benchmark scenario '{ScenarioName}'.");
        topLevelNodeModel = sceneDocument.CreateNodeModel(SvgBenchmarkHelpers.GetTopLevelNode(sceneDocument))
            ?? throw new InvalidOperationException($"Failed to create top-level node model for benchmark scenario '{ScenarioName}'.");
        leafNodeModel = sceneDocument.CreateNodeModel(SvgBenchmarkHelpers.GetLeafNode(sceneDocument))
            ?? throw new InvalidOperationException($"Failed to create leaf node model for benchmark scenario '{ScenarioName}'.");
        emptyModel = new SKPicture(fullModel.CullRect, new List<CanvasCommand>());
        fullModelCullRect = skiaModel.ToSKRect(fullModel.CullRect);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NativePicture", "Full")]
    public float CreateNativePictureFromFullModel()
    {
        using var picture = skiaModel!.ToSKPicture(fullModel!);
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
    [BenchmarkCategory("NativePicture", "LeafNode")]
    public float CreateNativePictureFromLeafNodeModel()
    {
        using var picture = skiaModel!.ToSKPicture(leafNodeModel!);
        return picture?.CullRect.Width ?? 0f;
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
