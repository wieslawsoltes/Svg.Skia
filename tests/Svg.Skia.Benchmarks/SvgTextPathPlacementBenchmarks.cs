using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using ShimSkiaSharp;
using Svg.Model;

namespace Svg.Skia.Benchmarks;

public class SvgTextPathPlacementBenchmarks
{
    private SvgTextPath[] textPaths = Array.Empty<SvgTextPath>();
    private SvgSceneTextCompiler.TextPathCodepointPlacementBenchmarkInput[] placementInputs = Array.Empty<SvgSceneTextCompiler.TextPathCodepointPlacementBenchmarkInput>();
    private SkiaSvgAssetLoader? assetLoader;
    private SKRect viewport;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names
        .Where(static name => name.Contains("text-path", StringComparison.OrdinalIgnoreCase));

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        var document = SvgBenchmarkHelpers.ParseDocument(scenario);
        viewport = SvgBenchmarkHelpers.GetDocumentViewport(document);
        assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        textPaths = document.Descendants()
            .OfType<SvgTextPath>()
            .Where(static textPath => !string.IsNullOrWhiteSpace(textPath.Content))
            .ToArray();

        if (textPaths.Length == 0)
        {
            throw new InvalidOperationException($"Scenario '{ScenarioName}' did not contain any textPath fragments.");
        }

        placementInputs = textPaths
            .Select(textPath => SvgSceneTextCompiler.CreateTextPathCodepointPlacementBenchmarkInput(textPath, textPath.Content!, viewport, assetLoader))
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Compile", "Text", "Placements", "TextPath", "Geometry")]
    public int ResolveTextPathGeometryAcrossFragments()
    {
        var totalSamples = 0;
        for (var i = 0; i < textPaths.Length; i++)
        {
            totalSamples += SvgSceneTextCompiler.BenchmarkResolveTextPathGeometry(textPaths[i], viewport);
        }

        return totalSamples;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Placements", "TextPath", "Prepare")]
    public int PrepareTextPathPlacementInputsAcrossFragments()
    {
        var totalVisibleGlyphs = 0;
        for (var i = 0; i < textPaths.Length; i++)
        {
            var input = SvgSceneTextCompiler.CreateTextPathCodepointPlacementBenchmarkInput(textPaths[i], textPaths[i].Content!, viewport, assetLoader!);
            totalVisibleGlyphs += input.VisibleGlyphCount;
        }

        return totalVisibleGlyphs;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Placements", "TextPath", "Midpoints")]
    public float ResolveTextPathMidpointsAcrossFragments()
    {
        var checksum = 0f;
        for (var i = 0; i < placementInputs.Length; i++)
        {
            checksum += SvgSceneTextCompiler.BenchmarkTextPathMidpointLookup(placementInputs[i]);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Placements", "TextPath", "Emit")]
    public float EmitTextPathPlacementsFromResolvedMidpointsAcrossFragments()
    {
        var checksum = 0f;
        for (var i = 0; i < placementInputs.Length; i++)
        {
            checksum += SvgSceneTextCompiler.BenchmarkTextPathPlacementEmission(placementInputs[i]);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Placements", "TextPath", "Placement")]
    public int CreateTextPathPlacementsFromPrebuiltGeometryAcrossFragments()
    {
        var totalPlacements = 0;
        for (var i = 0; i < placementInputs.Length; i++)
        {
            if (SvgSceneTextCompiler.TryBenchmarkTextPathCodepointPlacements(placementInputs[i], assetLoader!, out var placementCount, out _))
            {
                totalPlacements += placementCount;
            }
        }

        return totalPlacements;
    }
}
