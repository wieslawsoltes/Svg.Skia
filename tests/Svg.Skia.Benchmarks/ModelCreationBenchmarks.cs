using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using Svg.Model;
using Svg.Model.Services;
using Svg.Skia;
using ShimSkiaSharp;

namespace Svg.Skia.Benchmarks;

[MemoryDiagnoser]
public class ModelCreationBenchmarks
{
    private SkiaModel _skiaModel = null!;
    private ISvgAssetLoader _assetLoader = null!;
    private SvgDocument _document = null!;
    private string _svgText = string.Empty;
    private byte[] _svgBytes = Array.Empty<byte>();

    [ParamsSource(nameof(SvgNames))]
    public string SvgName { get; set; } = string.Empty;

    public IEnumerable<string> SvgNames => BenchmarkAssets.SvgNames;

    [GlobalSetup]
    public void Setup()
    {
        _skiaModel = new SkiaModel(new SKSvgSettings());
        _assetLoader = new SkiaSvgAssetLoader(_skiaModel);
        _svgText = BenchmarkAssets.GetSvgText(SvgName);
        _svgBytes = BenchmarkAssets.GetSvgBytes(SvgName);
        _document = SvgService.FromSvg(_svgText) ?? throw new InvalidOperationException("Failed to parse SVG document.");
    }

    [Benchmark(Baseline = true)]
    public SKPicture? ToModel_FromDocument()
    {
        return SvgService.ToModel(_document, _assetLoader, out _, out _);
    }

    [Benchmark]
    public SKPicture? ToModel_FromSvgString()
    {
        var document = SvgService.FromSvg(_svgText);
        if (document is null)
        {
            return null;
        }

        return SvgService.ToModel(document, _assetLoader, out _, out _);
    }

    [Benchmark]
    public SKPicture? ToModel_FromStream()
    {
        using var stream = new MemoryStream(_svgBytes);
        var document = SvgService.Open(stream);
        if (document is null)
        {
            return null;
        }

        return SvgService.ToModel(document, _assetLoader, out _, out _);
    }
}
