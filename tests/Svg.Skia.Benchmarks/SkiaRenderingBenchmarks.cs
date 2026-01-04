using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Svg.Model.Services;
using Svg.Skia;
using ShimSkiaSharp;

namespace Svg.Skia.Benchmarks;

[MemoryDiagnoser]
public class SkiaRenderingBenchmarks
{
    private SkiaModel _skiaModel = null!;
    private ISvgAssetLoader _assetLoader = null!;
    private SKPicture _model = null!;

    [ParamsSource(nameof(SvgNames))]
    public string SvgName { get; set; } = string.Empty;

    public IEnumerable<string> SvgNames => BenchmarkAssets.SvgNames;

    [GlobalSetup]
    public void Setup()
    {
        _skiaModel = new SkiaModel(new SKSvgSettings());
        _assetLoader = new SkiaSvgAssetLoader(_skiaModel);
        var svgText = BenchmarkAssets.GetSvgText(SvgName);
        var document = SvgService.FromSvg(svgText) ?? throw new InvalidOperationException("Failed to parse SVG document.");
        _model = SvgService.ToModel(document, _assetLoader, out _, out _) ?? throw new InvalidOperationException("Failed to build SVG model.");
    }

    [Benchmark(Baseline = true)]
    public void ToSKPicture()
    {
        using var picture = _skiaModel.ToSKPicture(_model);
    }

    [Benchmark]
    public void ToWireframePicture()
    {
        using var picture = _skiaModel.ToWireframePicture(_model);
    }

    [Benchmark]
    public void DrawToCanvas()
    {
        var skRect = new SkiaSharp.SKRect(0f, 0f, _model.CullRect.Width, _model.CullRect.Height);
        using var recorder = new SkiaSharp.SKPictureRecorder();
        using var canvas = recorder.BeginRecording(skRect);
        _skiaModel.Draw(_model, canvas);
        using var picture = recorder.EndRecording();
    }
}
