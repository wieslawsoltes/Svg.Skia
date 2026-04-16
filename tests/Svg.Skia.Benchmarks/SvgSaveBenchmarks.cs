using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using SkiaSharp;

namespace Svg.Skia.Benchmarks;

public class SvgSaveBenchmarks
{
    private SKSvg? svg;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        svg = new SKSvg();

        if (scenario.BaseUri is { } baseUri)
        {
            var bytes = Encoding.UTF8.GetBytes(scenario.SvgText);
            using var stream = new MemoryStream(bytes);
            if (svg.Load(stream, parameters: null, baseUri) is null)
            {
                throw new InvalidOperationException($"Failed to load benchmark scenario '{ScenarioName}'.");
            }

            return;
        }

        if (svg.FromSvg(scenario.SvgText) is null)
        {
            throw new InvalidOperationException($"Failed to load benchmark scenario '{ScenarioName}'.");
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        svg?.Dispose();
    }

    [Benchmark]
    [BenchmarkCategory("Save", "Encode", "Png", "SKSvg", "1x")]
    public long SaveTransparentPng1x()
    {
        using var stream = new MemoryStream();
        return svg!.Save(stream, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1f, 1f)
            ? stream.Length
            : -1;
    }

    [IterationSetup(Target = nameof(SaveTransparentPng1xAfterPictureRefresh))]
    public void SetupSaveTransparentPng1xAfterPictureRefresh()
    {
        _ = svg!.Model;
        _ = svg.RebuildFromModel();
    }

    [Benchmark]
    [BenchmarkCategory("Save", "Encode", "Png", "SKSvg", "1x", "ColdPicture")]
    public long SaveTransparentPng1xAfterPictureRefresh()
    {
        using var stream = new MemoryStream();
        return svg!.Save(stream, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1f, 1f)
            ? stream.Length
            : -1;
    }
}
