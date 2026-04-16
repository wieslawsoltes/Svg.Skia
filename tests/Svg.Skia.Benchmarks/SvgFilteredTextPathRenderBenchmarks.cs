using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Svg.Model;

namespace Svg.Skia.Benchmarks;

public class SvgFilteredTextPathRenderBenchmarks
{
    private SvgSceneTextCompiler.FilteredTextPathRunBenchmarkInput[] filteredRuns = Array.Empty<SvgSceneTextCompiler.FilteredTextPathRunBenchmarkInput>();
    private SkiaSvgAssetLoader? assetLoader;

    [Params(24, 96)]
    public int TextPathCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(BuildFilteredTextPathScene(TextPathCount));
        var viewport = SvgBenchmarkHelpers.GetDocumentViewport(document);
        assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        filteredRuns = document.Descendants()
            .OfType<SvgTextPath>()
            .SelectMany(textPath => SvgSceneTextCompiler.CreateFilteredTextPathRunBenchmarkInputs(textPath, viewport, assetLoader))
            .ToArray();

        if (filteredRuns.Length == 0)
        {
            throw new InvalidOperationException($"Generated benchmark scene did not produce any filtered textPath runs for count {TextPathCount}.");
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Render", "Text", "TextPath", "Filter")]
    public int DrawFilteredTextPathRunsAcrossFragments()
    {
        var totalCommands = 0;
        for (var i = 0; i < filteredRuns.Length; i++)
        {
            totalCommands += SvgSceneTextCompiler.BenchmarkDrawFilteredTextPathRun(filteredRuns[i], assetLoader!);
        }

        return totalCommands;
    }

    private static string BuildFilteredTextPathScene(int textPathCount)
    {
        const int columns = 4;
        var rows = (textPathCount + columns - 1) / columns;
        var width = 420;
        var height = Math.Max(80, (rows * 44) + 24);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        builder.AppendLine("""
  <defs>
    <filter id="blur-filter" x="-20%" y="-20%" width="140%" height="140%">
      <feGaussianBlur stdDeviation="1.5" />
    </filter>
""");

        for (var i = 0; i < textPathCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = 12 + (column * 100);
            var y = 22 + (row * 44);
            builder.AppendLine($"""    <path id="curve-{i}" d="M{x},{y + 12} C{x + 18},{y - 4} {x + 58},{y + 28} {x + 82},{y + 12}" />""");
        }

        builder.AppendLine("  </defs>");

        for (var i = 0; i < textPathCount; i++)
        {
            var fill = i % 2 == 0 ? "#1b4d9b" : "#9b3f1b";
            builder.AppendLine($"""  <text font-size="10" fill="{fill}">""");
            builder.AppendLine($"""    <textPath href="#curve-{i}" filter="url(#blur-filter)">Filtered path {i}</textPath>""");
            builder.AppendLine("  </text>");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }
}
