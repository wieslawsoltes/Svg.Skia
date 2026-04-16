using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using ShimSkiaSharp;

namespace Svg.Skia.Benchmarks;

public class SvgTextAssetLoaderBenchmarks
{
    private SkiaSvgAssetLoader? assetLoader;
    private SKPaint? textPaint;
    private string[]? textSamples;
    private string repeatedText = string.Empty;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios
        .Names
        .Where(static name =>
            name.Contains("text", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("file:", StringComparison.OrdinalIgnoreCase));

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        var document = SvgBenchmarkHelpers.ParseDocument(scenario);

        textSamples = document
            .Descendants()
            .OfType<SvgText>()
            .Select(static text => text.Content)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .Take(64)
            .ToArray();

        if (textSamples.Length == 0)
        {
            textSamples = ["Svg.Skia benchmark text"];
        }

        repeatedText = textSamples[0];
        assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        textPaint = new SKPaint
        {
            TextSize = 16f,
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Compile", "Text", "MeasureText", "Single")]
    public float MeasureTextSingle()
    {
        var bounds = default(SKRect);
        return assetLoader!.MeasureText(repeatedText, textPaint!, ref bounds);
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "MeasureText", "Sequence")]
    public float MeasureTextSequence()
    {
        var totalAdvance = 0f;
        var bounds = default(SKRect);
        var samples = textSamples!;

        for (var i = 0; i < samples.Length; i++)
        {
            totalAdvance += assetLoader!.MeasureText(samples[i], textPaint!, ref bounds);
        }

        return totalAdvance;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "FontMetrics", "Sequence")]
    public float GetFontMetricsSequence()
    {
        var totalAscent = 0f;
        var samples = textSamples!;

        for (var i = 0; i < samples.Length; i++)
        {
            totalAscent += assetLoader!.GetFontMetrics(textPaint!).Ascent;
        }

        return totalAscent;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "GlyphShaping", "Sequence")]
    public float TryShapeGlyphRunSequence()
    {
        var totalAdvance = 0f;
        var samples = textSamples!;

        for (var i = 0; i < samples.Length; i++)
        {
            if (assetLoader!.TryShapeGlyphRun(samples[i], textPaint!, out var shapedRun))
            {
                totalAdvance += shapedRun.Advance;
            }
        }

        return totalAdvance;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "FindTypefaces", "Single")]
    public float FindTypefacesSingle()
    {
        var spans = assetLoader!.FindTypefaces(repeatedText, textPaint!);
        var totalAdvance = 0f;
        for (var i = 0; i < spans.Count; i++)
        {
            totalAdvance += spans[i].Advance;
        }

        return totalAdvance;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "FindTypefaces", "Sequence")]
    public float FindTypefacesSequence()
    {
        var totalAdvance = 0f;
        var samples = textSamples!;

        for (var i = 0; i < samples.Length; i++)
        {
            var spans = assetLoader!.FindTypefaces(samples[i], textPaint!);
            for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
            {
                totalAdvance += spans[spanIndex].Advance;
            }
        }

        return totalAdvance;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "FindTypefaces", "Single", "Cold")]
    public float FindTypefacesSingleCold()
    {
        var coldLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var spans = coldLoader.FindTypefaces(repeatedText, textPaint!);
        var totalAdvance = 0f;
        for (var i = 0; i < spans.Count; i++)
        {
            totalAdvance += spans[i].Advance;
        }

        return totalAdvance;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "FindTypefaces", "Sequence", "Cold")]
    public float FindTypefacesSequenceCold()
    {
        var coldLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var totalAdvance = 0f;
        var samples = textSamples!;

        for (var i = 0; i < samples.Length; i++)
        {
            var spans = coldLoader.FindTypefaces(samples[i], textPaint!);
            for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
            {
                totalAdvance += spans[spanIndex].Advance;
            }
        }

        return totalAdvance;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "FontMetrics", "Sequence", "Cold")]
    public float GetFontMetricsSequenceCold()
    {
        var coldLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var totalAscent = 0f;
        var samples = textSamples!;

        for (var i = 0; i < samples.Length; i++)
        {
            totalAscent += coldLoader.GetFontMetrics(textPaint!).Ascent;
        }

        return totalAscent;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "GlyphShaping", "Sequence", "Cold")]
    public float TryShapeGlyphRunSequenceCold()
    {
        var coldLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var totalAdvance = 0f;
        var samples = textSamples!;

        for (var i = 0; i < samples.Length; i++)
        {
            if (coldLoader.TryShapeGlyphRun(samples[i], textPaint!, out var shapedRun))
            {
                totalAdvance += shapedRun.Advance;
            }
        }

        return totalAdvance;
    }
}
