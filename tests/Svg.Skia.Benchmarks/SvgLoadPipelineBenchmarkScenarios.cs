using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Svg.Skia.Benchmarks;

internal static class SvgLoadPipelineBenchmarkScenarios
{
    private const string ExternalPathsEnvVar = "SVG_SKIA_BENCHMARK_SVG_PATHS";
    private static readonly Lazy<IReadOnlyList<SvgLoadPipelineBenchmarkScenario>> ScenariosCache = new(CreateScenarios);

    public static IEnumerable<string> Names => ScenariosCache.Value.Select(static scenario => scenario.Name);

    public static SvgLoadPipelineBenchmarkScenario Resolve(string name)
    {
        return ScenariosCache.Value.First(scenario => string.Equals(scenario.Name, name, StringComparison.Ordinal));
    }

    private static IReadOnlyList<SvgLoadPipelineBenchmarkScenario> CreateScenarios()
    {
        var scenarios = new List<SvgLoadPipelineBenchmarkScenario>
        {
            new("generated-inline-styles-512", BuildInlineStyleScene(512), null),
            new("generated-gradients-512", BuildGradientScene(512), null),
            new("generated-filtered-shapes-256", BuildFilteredShapeScene(256), null),
            new("generated-text-192", BuildTextScene(192), null),
            new("generated-shapes-1024", BuildShapeScene(1024), null)
        };

        var externalPaths = Environment.GetEnvironmentVariable(ExternalPathsEnvVar);
        if (string.IsNullOrWhiteSpace(externalPaths))
        {
            return scenarios;
        }

        var externalScenarioCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in externalPaths.Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fullPath = Path.GetFullPath(candidate);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Benchmark SVG file configured via {ExternalPathsEnvVar} was not found.", fullPath);
            }

            var fileName = Path.GetFileName(fullPath);
            var scenarioName = $"file:{fileName}";
            if (externalScenarioCounts.TryGetValue(scenarioName, out var suffix))
            {
                suffix++;
                externalScenarioCounts[scenarioName] = suffix;
                scenarioName = $"{scenarioName}#{suffix}";
            }
            else
            {
                externalScenarioCounts[scenarioName] = 0;
            }

            scenarios.Add(new SvgLoadPipelineBenchmarkScenario(
                scenarioName,
                File.ReadAllText(fullPath),
                new Uri(fullPath)));
        }

        return scenarios;
    }

    private static string BuildInlineStyleScene(int elementCount)
    {
        const int columns = 32;
        var rows = (elementCount + columns - 1) / columns;
        var width = (columns * 24) + 24;
        var height = (rows * 24) + 24;
        var builder = CreateSvgBuilder(width, height);

        for (var i = 0; i < elementCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = 8 + (column * 24);
            var y = 8 + (row * 24);
            var fill = $"rgb({(i * 29) % 255},{(i * 41) % 255},{(i * 53) % 255})";
            var stroke = $"rgb({(i * 17) % 255},{(i * 23) % 255},{(i * 31) % 255})";
            builder.AppendLine(
                $"""  <rect id="rect-{i}" x="{x}" y="{y}" width="18" height="18" style="/*lead*/FiLl:{fill}; /*mid*/ stroke:{stroke}; stroke-width:1; opacity:0.9/*tail*/" />""");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildTextScene(int textNodeCount)
    {
        const int columns = 4;
        var rows = (textNodeCount + columns - 1) / columns;
        var width = 960;
        var height = (rows * 28) + 32;
        var builder = CreateSvgBuilder(width, height);
        builder.AppendLine("""  <g font-family="Noto Sans" font-size="16">""");

        for (var i = 0; i < textNodeCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = 16 + (column * 220);
            var y = 24 + (row * 28);
            var accent = i % 2 == 0 ? "royalblue" : "darkorange";
            builder.AppendLine(
                $"""    <text id="text-{i}" x="{x}" y="{y}" fill="black">Item <tspan fill="{accent}">{i}</tspan> sample <tspan font-style="italic">glyphs</tspan></text>""");
        }

        builder.AppendLine("  </g>");
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildGradientScene(int elementCount)
    {
        const int columns = 32;
        var rows = (elementCount + columns - 1) / columns;
        var width = (columns * 24) + 24;
        var height = (rows * 24) + 24;
        var builder = CreateSvgBuilder(width, height);
        builder.AppendLine("""
          <defs>
            <linearGradient id="shared-gradient" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stop-color="#0f172a" />
              <stop offset="0.5" stop-color="#2563eb" />
              <stop offset="1" stop-color="#22c55e" />
            </linearGradient>
          </defs>
        """);

        for (var i = 0; i < elementCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = 8 + (column * 24);
            var y = 8 + (row * 24);
            var rotation = (i % 11) - 5;
            builder.AppendLine(
                $"""  <rect id="gradient-{i}" x="{x}" y="{y}" width="18" height="18" fill="url(#shared-gradient)" stroke="white" stroke-width="0.5" transform="rotate({rotation} {x + 9} {y + 9})" />""");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildFilteredShapeScene(int elementCount)
    {
        const int columns = 16;
        var rows = (elementCount + columns - 1) / columns;
        var width = (columns * 48) + 32;
        var height = (rows * 48) + 32;
        var builder = CreateSvgBuilder(width, height);
        builder.AppendLine("""
          <defs>
            <filter id="shared-blur" x="-20%" y="-20%" width="140%" height="140%">
              <feGaussianBlur stdDeviation="1.2" />
            </filter>
          </defs>
        """);

        for (var i = 0; i < elementCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = 16 + (column * 48);
            var y = 16 + (row * 48);
            var fill = (i % 4) switch
            {
                0 => "#2563eb",
                1 => "#22c55e",
                2 => "#eab308",
                _ => "#ef4444"
            };

            builder.AppendLine($"""  <g id="filtered-{i}" filter="url(#shared-blur)" transform="translate({x} {y})">""");
            builder.AppendLine($"""    <rect x="4" y="4" width="20" height="20" rx="4" fill="{fill}" opacity="0.9" />""");
            builder.AppendLine("""    <circle cx="28" cy="14" r="8" fill="white" opacity="0.35" />""");
            builder.AppendLine("""  </g>""");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildShapeScene(int elementCount)
    {
        const int columns = 32;
        var rows = (elementCount + columns - 1) / columns;
        var width = (columns * 24) + 24;
        var height = (rows * 24) + 24;
        var builder = CreateSvgBuilder(width, height);

        for (var i = 0; i < elementCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = 8 + (column * 24);
            var y = 8 + (row * 24);
            var fill = (i % 3) switch
            {
                0 => "seagreen",
                1 => "royalblue",
                _ => "crimson"
            };

            builder.AppendLine($"""  <g id="g-{i}" transform="translate({x} {y}) rotate({i % 15})">""");
            builder.AppendLine($"""    <rect x="0" y="0" width="14" height="14" fill="{fill}" opacity="0.85" />""");
            builder.AppendLine("""    <path d="M 0 14 L 7 0 L 14 14 Z" fill="white" opacity="0.25" />""");
            builder.AppendLine("""  </g>""");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static StringBuilder CreateSvgBuilder(int width, int height)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        return builder;
    }
}

internal sealed record SvgLoadPipelineBenchmarkScenario(string Name, string SvgText, Uri? BaseUri);
