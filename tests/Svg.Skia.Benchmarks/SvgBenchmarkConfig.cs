using System;
using System.Globalization;
using System.IO;
using System.Text;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;

namespace Svg.Skia.Benchmarks;

internal sealed class SvgBenchmarkConfig : ManualConfig
{
    private const string ArtifactsPathEnvironmentVariable = "SVG_SKIA_BENCHMARK_ARTIFACTS";
    private const string RunLabelEnvironmentVariable = "SVG_SKIA_BENCHMARK_RUN_LABEL";

    public SvgBenchmarkConfig()
        : this(ResolveArtifactsPath(args: null))
    {
    }

    public SvgBenchmarkConfig(string artifactsPath)
    {
        ArtifactsPath = artifactsPath;

        AddLogger(ConsoleLogger.Default);
        AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddColumn(RankColumn.Arabic);
        AddColumn(StatisticColumn.P95);
        AddJob(Job.ShortRun
            .WithId("ShortRun")
            .WithLaunchCount(1)
            .WithWarmupCount(3)
            .WithIterationCount(8));
        WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
        WithSummaryStyle(SummaryStyle.Default.WithCultureInfo(CultureInfo.InvariantCulture));

        Options |= ConfigOptions.JoinSummary;
    }

    public static string ResolveArtifactsPath(string[]? args)
    {
        var artifactsRoot = TryGetCommandLineArtifactsPath(args);
        if (!string.IsNullOrWhiteSpace(artifactsRoot))
        {
            return Path.GetFullPath(artifactsRoot);
        }

        artifactsRoot = Environment.GetEnvironmentVariable(ArtifactsPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(artifactsRoot))
        {
            artifactsRoot = Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "artifacts",
                "benchmarks");
        }

        var runLabel = Environment.GetEnvironmentVariable(RunLabelEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(runLabel))
        {
            artifactsRoot = Path.Combine(artifactsRoot, SanitizePathSegment(runLabel));
        }

        return Path.GetFullPath(artifactsRoot);
    }

    private static string? TryGetCommandLineArtifactsPath(string[]? args)
    {
        if (args is null)
        {
            return null;
        }

        for (var i = 0; i < args.Length; i++)
        {
            const string artifactsPrefix = "--artifacts=";
            if (args[i].StartsWith(artifactsPrefix, StringComparison.Ordinal))
            {
                return args[i][artifactsPrefix.Length..];
            }

            if (!string.Equals(args[i], "--artifacts", StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }

            return null;
        }

        return null;
    }

    private static string SanitizePathSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        var invalidCharacters = Path.GetInvalidFileNameChars();

        foreach (var character in value)
        {
            if (Array.IndexOf(invalidCharacters, character) >= 0 || char.IsControl(character))
            {
                builder.Append('-');
                continue;
            }

            builder.Append(char.IsWhiteSpace(character) ? '-' : character);
        }

        var segment = builder.ToString().Trim('-', '.', '_');
        return segment.Length == 0 ? "run" : segment;
    }
}
