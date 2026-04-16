using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using SkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia.Benchmarks;

public class SvgLoadPipelineBenchmarks
{
    private string svgText = string.Empty;
    private Uri? baseUri;
    private SkiaModel? stageSkiaModel;
    private SkiaSvgAssetLoader? stageAssetLoader;
    private SvgDocument? parsedDocument;
    private SvgSceneDocument? compiledScene;
    private ShimSkiaSharp.SKPicture? shimPicture;
    private SKPicture? nativePicture;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        svgText = scenario.SvgText;
        baseUri = scenario.BaseUri;
        stageSkiaModel = new SkiaModel(new SKSvgSettings());
        stageAssetLoader = new SkiaSvgAssetLoader(stageSkiaModel);
        parsedDocument = ParseDocument();

        if (!SvgSceneRuntime.TryCompile(parsedDocument, stageAssetLoader, DrawAttributes.None, out var sceneDocument) ||
            sceneDocument is null)
        {
            throw new InvalidOperationException($"Failed to compile retained scene for benchmark scenario '{ScenarioName}'.");
        }

        compiledScene = sceneDocument;
        shimPicture = compiledScene.CreateModel() ?? throw new InvalidOperationException(
            $"Failed to create shim picture model for benchmark scenario '{ScenarioName}'.");
        nativePicture = stageSkiaModel.ToSKPicture(shimPicture) ?? throw new InvalidOperationException(
            $"Failed to create native picture for benchmark scenario '{ScenarioName}'.");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        nativePicture?.Dispose();
    }

    [Benchmark]
    [BenchmarkCategory("Load", "Parse")]
    public int ParseSvgDocumentFromString()
    {
        var document = ParseDocument();
        return document.Children.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Load", "Compile")]
    public int CompileRetainedSceneFromParsedDocument()
    {
        var succeeded = SvgSceneRuntime.TryCompile(parsedDocument!, stageAssetLoader!, DrawAttributes.None, out var sceneDocument);
        return succeeded && sceneDocument is not null ? sceneDocument.Root.Children.Count : -1;
    }

    [Benchmark]
    [BenchmarkCategory("Load", "ShimModel")]
    public int CreateShimPictureModel()
    {
        var model = compiledScene!.CreateModel();
        return model?.Commands?.Count ?? -1;
    }

    [Benchmark]
    [BenchmarkCategory("Load", "NativePicture")]
    public float CreateNativeSkPicture()
    {
        using var picture = stageSkiaModel!.ToSKPicture(shimPicture!);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("Load", "Render")]
    public int RenderNativePictureToBitmap()
    {
        using var bitmap = nativePicture!.ToBitmap(
            SKColors.Transparent,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            stageSkiaModel!.Settings.Srgb);
        return bitmap?.ByteCount ?? 0;
    }

    [Benchmark]
    [BenchmarkCategory("Load", "Encode")]
    public long EncodeNativePictureToPng()
    {
        using var stream = new MemoryStream();
        return nativePicture!.ToImage(
            stream,
            SKColors.Transparent,
            SKEncodedImageFormat.Png,
            100,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            stageSkiaModel!.Settings.Srgb)
            ? stream.Length
            : -1;
    }

    [Benchmark]
    [BenchmarkCategory("Load", "EndToEnd")]
    public float LoadViaSkSvg()
    {
        using var svg = new SKSvg();
        if (baseUri is { } scenarioBaseUri)
        {
            var bytes = Encoding.UTF8.GetBytes(svgText);
            using var stream = new MemoryStream(bytes);
            using var picture = svg.Load(stream, parameters: null, scenarioBaseUri);
            return picture?.CullRect.Width ?? 0f;
        }

        using var inlinePicture = svg.FromSvg(svgText);
        return inlinePicture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("Load", "EndToEnd", "StringSource", "ControlLike")]
    public float LoadViaSkSvgFromStringWithBaseUri()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvg(svgText, parameters: null, baseUri);
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("Load", "EndToEnd", "ShimModel")]
    public int LoadViaSkSvgAndAccessModel()
    {
        using var svg = new SKSvg();
        if (baseUri is { } scenarioBaseUri)
        {
            var bytes = Encoding.UTF8.GetBytes(svgText);
            using var stream = new MemoryStream(bytes);
            using var picture = svg.Load(stream, parameters: null, scenarioBaseUri);
            return svg.Model?.Commands?.Count ?? -1;
        }

        using var inlinePicture = svg.FromSvg(svgText);
        return svg.Model?.Commands?.Count ?? -1;
    }

    private SvgDocument ParseDocument()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svgText);
        if (baseUri is not null)
        {
            document.BaseUri = baseUri;
        }

        return document;
    }
}
