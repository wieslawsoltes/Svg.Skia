using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using SkiaSharp;
using Svg.Model;
using ShimCanvasCommand = ShimSkiaSharp.CanvasCommand;
using ShimPicture = ShimSkiaSharp.SKPicture;

namespace Svg.Skia.Benchmarks;

public class SvgRenderBitmapBenchmarks
{
    private SkiaModel? skiaModel;
    private SKPicture? nativePicture;
    private SKPicture? emptyNativePicture;
    private SKBitmap? reusableBitmap1x;
    private SKBitmap? reusableBitmap2x;
    private SKCanvas? reusableCanvas1x;
    private SKCanvas? reusableCanvas2x;
    private SKSurface? reusableSurface1x;
    private SKImageInfo imageInfo1x;
    private SKImageInfo imageInfo2x;
    private SKMatrix scaleMatrix2x;

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

        var model = sceneDocument.CreateModel() ?? throw new InvalidOperationException($"Failed to create model for benchmark scenario '{ScenarioName}'.");
        nativePicture = skiaModel.ToSKPicture(model) ?? throw new InvalidOperationException($"Failed to create native picture for benchmark scenario '{ScenarioName}'.");
        var emptyModel = new ShimPicture(model.CullRect, new List<ShimCanvasCommand>());
        emptyNativePicture = skiaModel.ToSKPicture(emptyModel) ?? throw new InvalidOperationException($"Failed to create empty native picture for benchmark scenario '{ScenarioName}'.");
        if (!nativePicture.TryGetImageInfo(1f, 1f, SKColorType.Rgba8888, SKAlphaType.Premul, skiaModel.Settings.Srgb, out imageInfo1x) ||
            !nativePicture.TryGetImageInfo(2f, 2f, SKColorType.Rgba8888, SKAlphaType.Premul, skiaModel.Settings.Srgb, out imageInfo2x))
        {
            throw new InvalidOperationException($"Failed to create image info for benchmark scenario '{ScenarioName}'.");
        }

        reusableBitmap1x = new SKBitmap(imageInfo1x);
        reusableBitmap2x = new SKBitmap(imageInfo2x);
        reusableCanvas1x = new SKCanvas(reusableBitmap1x);
        reusableCanvas2x = new SKCanvas(reusableBitmap2x);
        reusableSurface1x = SKSurface.Create(imageInfo1x) ?? throw new InvalidOperationException($"Failed to create reusable surface for benchmark scenario '{ScenarioName}'.");
        scaleMatrix2x = SKMatrix.CreateScale(2f, 2f);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        reusableSurface1x?.Dispose();
        reusableCanvas1x?.Dispose();
        reusableCanvas2x?.Dispose();
        reusableBitmap1x?.Dispose();
        reusableBitmap2x?.Dispose();
        emptyNativePicture?.Dispose();
        nativePicture?.Dispose();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Render", "Allocate", "1x")]
    public int AllocateBitmapCanvas1x()
    {
        using var bitmap = new SKBitmap(imageInfo1x);
        using var canvas = new SKCanvas(bitmap);
        return bitmap.ByteCount;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "Clear", "1x")]
    public int ClearReusableBitmapCanvas1x()
    {
        reusableCanvas1x!.Clear(SKColors.Transparent);
        return reusableBitmap1x!.ByteCount;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "Draw", "Empty", "1x")]
    public int DrawEmptyNativePicture1x()
    {
        reusableCanvas1x!.Clear(SKColors.Transparent);
        reusableCanvas1x.DrawPicture(emptyNativePicture!);
        return reusableBitmap1x!.ByteCount;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "Draw", "Picture", "1x")]
    public int DrawNativePicture1x()
    {
        reusableCanvas1x!.Clear(SKColors.Transparent);
        reusableCanvas1x.DrawPicture(nativePicture!);
        return reusableBitmap1x!.ByteCount;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "Draw", "Picture", "2x", "SaveScaleRestore")]
    public int DrawNativePicture2xWithSaveScaleRestore()
    {
        reusableCanvas2x!.Clear(SKColors.Transparent);
        reusableCanvas2x.Save();
        reusableCanvas2x.Scale(2f, 2f);
        reusableCanvas2x.DrawPicture(nativePicture!);
        reusableCanvas2x.Restore();
        return reusableBitmap2x!.ByteCount;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "Draw", "Picture", "2x", "MatrixOverload")]
    public int DrawNativePicture2xWithMatrixOverload()
    {
        reusableCanvas2x!.Clear(SKColors.Transparent);
        reusableCanvas2x.DrawPicture(nativePicture!, in scaleMatrix2x, null);
        return reusableBitmap2x!.ByteCount;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "ToBitmap", "Transparent", "1x")]
    public int RenderTransparentBitmap1x()
    {
        using var bitmap = nativePicture!.ToBitmap(
            SKColors.Transparent,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            skiaModel!.Settings.Srgb);
        return bitmap?.ByteCount ?? 0;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "ToBitmap", "Transparent", "Reusable", "1x")]
    public int RenderTransparentBitmap1xIntoReusableBitmap()
    {
        return nativePicture!.ToBitmap(
            reusableBitmap1x!,
            reusableCanvas1x!,
            SKColors.Transparent,
            1f,
            1f)
            ? reusableBitmap1x!.ByteCount
            : 0;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "ToBitmap", "Empty", "1x")]
    public int RenderEmptyBitmap1x()
    {
        using var bitmap = emptyNativePicture!.ToBitmap(
            SKColors.Transparent,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            skiaModel!.Settings.Srgb);
        return bitmap?.ByteCount ?? 0;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "Encode", "Png", "AllocateSurface", "1x")]
    public long EncodeTransparentBitmap1x()
    {
        using var stream = new System.IO.MemoryStream();
        return nativePicture!.ToImage(
            stream,
            SKColors.Transparent,
            SKEncodedImageFormat.Png,
            100,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            skiaModel!.Settings.Srgb)
            ? stream.Length
            : -1;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "Encode", "Png", "ReusableSurface", "1x")]
    public long EncodeTransparentBitmap1xViaReusableSurface()
    {
        using var stream = new System.IO.MemoryStream();
        return nativePicture!.ToImage(
            stream,
            reusableSurface1x!,
            SKColors.Transparent,
            SKEncodedImageFormat.Png,
            100,
            1f,
            1f)
            ? stream.Length
            : -1;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "ToBitmap", "Opaque", "1x")]
    public int RenderOpaqueBitmap1x()
    {
        using var bitmap = nativePicture!.ToBitmap(
            SKColors.White,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            skiaModel!.Settings.Srgb);
        return bitmap?.ByteCount ?? 0;
    }

    [Benchmark]
    [BenchmarkCategory("Render", "ToBitmap", "Transparent", "2x")]
    public int RenderTransparentBitmap2x()
    {
        using var bitmap = nativePicture!.ToBitmap(
            SKColors.Transparent,
            2f,
            2f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            skiaModel!.Settings.Srgb);
        return bitmap?.ByteCount ?? 0;
    }

}
