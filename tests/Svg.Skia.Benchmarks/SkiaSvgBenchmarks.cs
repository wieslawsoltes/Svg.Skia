using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Svg.Skia;

namespace Svg.Skia.Benchmarks;

[MemoryDiagnoser]
public class SkiaSvgBenchmarks
{
    private string _svgText = string.Empty;
    private byte[] _svgBytes = Array.Empty<byte>();
    private SKSvg _skSvg = null!;

    [ParamsSource(nameof(SvgNames))]
    public string SvgName { get; set; } = string.Empty;

    public IEnumerable<string> SvgNames => BenchmarkAssets.SvgNames;

    [GlobalSetup]
    public void Setup()
    {
        _svgText = BenchmarkAssets.GetSvgText(SvgName);
        _svgBytes = BenchmarkAssets.GetSvgBytes(SvgName);
        _skSvg = new SKSvg();
        _skSvg.FromSvg(_svgText);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _skSvg.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void LoadFromSvg()
    {
        using var skSvg = SKSvg.CreateFromSvg(_svgText);
    }

    [Benchmark]
    public void LoadFromStream()
    {
        using var stream = new System.IO.MemoryStream(_svgBytes);
        using var skSvg = SKSvg.CreateFromStream(stream);
    }

    [Benchmark]
    public void RebuildFromModel()
    {
        _skSvg.RebuildFromModel();
    }
}
