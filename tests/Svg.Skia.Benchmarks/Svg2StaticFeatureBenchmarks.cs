using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia.Benchmarks;

public class Svg2StaticFeatureBenchmarks
{
    private string svgText = string.Empty;
    private SvgDocument? parsedDocument;
    private SkiaSvgAssetLoader? assetLoader;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => FeatureScenarios.Select(static scenario => scenario.Name);

    private static IReadOnlyList<SvgLoadPipelineBenchmarkScenario> FeatureScenarios { get; } =
    [
        new("svg2-css-geometry-768", BuildCssGeometryScene(768), null),
        new("svg2-use-inherited-markers-512", BuildUseInheritedMarkerScene(512), null),
        new("svg2-symbol-dimensions-512", BuildSymbolDimensionScene(512), null),
        new("svg2-textpath-side-right-128", BuildTextPathSideRightScene(128), null)
    ];

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = FeatureScenarios.First(candidate => string.Equals(candidate.Name, ScenarioName, StringComparison.Ordinal));
        svgText = scenario.SvgText;
        parsedDocument = SvgBenchmarkHelpers.ParseDocument(scenario);
        assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Svg2Static", "Parse")]
    public int ParseSvgDocumentFromString()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svgText);
        return document.Descendants().Count();
    }

    [Benchmark]
    [BenchmarkCategory("Svg2Static", "Compile")]
    public int CompileRetainedSceneFromParsedDocument()
    {
        var succeeded = SvgSceneRuntime.TryCompile(parsedDocument!, assetLoader!, DrawAttributes.None, out var sceneDocument);
        return succeeded && sceneDocument is not null ? sceneDocument.Traverse().Count() : -1;
    }

    [Benchmark]
    [BenchmarkCategory("Svg2Static", "EndToEnd")]
    public float LoadViaSkSvg()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvg(svgText);
        return picture?.CullRect.Width ?? 0f;
    }

    private static string BuildCssGeometryScene(int elementCount)
    {
        const int columns = 32;
        var rows = (elementCount + columns - 1) / columns;
        var builder = CreateSvgBuilder((columns * 22) + 16, (rows * 22) + 16);
        builder.AppendLine("  <style>");

        for (var i = 0; i < elementCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = 8 + (column * 22);
            var y = 8 + (row * 22);
            builder.AppendLine(
                FormattableString.Invariant(
                    $"    #css-rect-{i} {{ x: {x}px; y: {y}px; width: 14px; height: 14px; rx: 2px; fill: rgb({(i * 17) % 255}, {(i * 29) % 255}, {(i * 41) % 255}); }}"));
        }

        builder.AppendLine("  </style>");
        for (var i = 0; i < elementCount; i++)
        {
            builder.AppendLine($"""  <rect id="css-rect-{i}" width="1" height="1" />""");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildUseInheritedMarkerScene(int useCount)
    {
        const int columns = 16;
        var rows = (useCount + columns - 1) / columns;
        var builder = CreateSvgBuilder((columns * 44) + 24, (rows * 24) + 24);
        builder.AppendLine("""
          <defs>
            <marker id="marker-a" markerWidth="6" markerHeight="6" refX="3" refY="3" markerUnits="userSpaceOnUse">
              <circle cx="3" cy="3" r="3" fill="#ef4444" />
            </marker>
            <marker id="marker-b" markerWidth="6" markerHeight="6" refX="3" refY="3" markerUnits="userSpaceOnUse">
              <rect width="6" height="6" fill="#2563eb" />
            </marker>
            <path id="segment-template" d="M4 10 L32 10" fill="none" stroke="#111827" stroke-width="1" />
          </defs>
        """);

        for (var i = 0; i < useCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var marker = i % 2 == 0 ? "marker-a" : "marker-b";
            builder.AppendLine(
                $"""  <use id="use-{i}" href="#segment-template" x="{8 + (column * 44)}" y="{8 + (row * 24)}" style="marker-start:url(#{marker}); marker-end:url(#{marker})" />""");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildSymbolDimensionScene(int useCount)
    {
        const int columns = 32;
        var rows = (useCount + columns - 1) / columns;
        var builder = CreateSvgBuilder((columns * 24) + 16, (rows * 18) + 16);
        builder.AppendLine("""
          <defs>
            <symbol id="tile-symbol" width="16" height="10" viewBox="0 0 16 10">
              <rect width="16" height="10" fill="#22c55e" />
              <path d="M0 10 L8 0 L16 10 Z" fill="#ffffff" opacity="0.45" />
            </symbol>
          </defs>
        """);

        for (var i = 0; i < useCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            builder.AppendLine($"""  <use id="symbol-use-{i}" href="#tile-symbol" x="{8 + (column * 24)}" y="{8 + (row * 18)}" />""");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildTextPathSideRightScene(int textPathCount)
    {
        const int columns = 2;
        var rows = (textPathCount + columns - 1) / columns;
        var builder = CreateSvgBuilder(1080, (rows * 72) + 48);
        builder.AppendLine("  <defs>");

        for (var i = 0; i < textPathCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = 24 + (column * 520);
            var y = 48 + (row * 72);
            builder.AppendLine(FormattableString.Invariant($"""    <path id="side-path-{i}" d="{BuildWavePathData(x, y)}" />"""));
        }

        builder.AppendLine("  </defs>");
        builder.AppendLine("""  <g font-family="Noto Sans" font-size="14" fill="#111827">""");

        for (var i = 0; i < textPathCount; i++)
        {
            var startOffset = ((i % 6) * 6).ToString(CultureInfo.InvariantCulture);
            builder.AppendLine($"""    <text id="side-text-{i}"><textPath href="#side-path-{i}" side="right" startOffset="{startOffset}%">SVG 2 side right textPath placement sample {i}</textPath></text>""");
        }

        builder.AppendLine("  </g>");
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static StringBuilder CreateSvgBuilder(int width, int height)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        return builder;
    }

    private static string BuildWavePathData(int originX, int originY)
    {
        var builder = new StringBuilder();
        builder.Append(FormattableString.Invariant($"M {originX} {originY}"));
        for (var segment = 0; segment < 8; segment++)
        {
            var x0 = originX + (segment * 56);
            var x1 = x0 + 28;
            var x2 = x0 + 56;
            var crestY = originY + (segment % 2 == 0 ? -18 : 18);
            var troughY = originY + (segment % 2 == 0 ? 16 : -16);
            builder.Append(FormattableString.Invariant($" C {x0 + 14} {crestY} {x1} {troughY} {x2} {originY}"));
        }

        return builder.ToString();
    }
}
