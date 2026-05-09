using System;
using System.Globalization;
using System.IO;
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
    public SvgBenchmarkConfig()
    {
        ArtifactsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "artifacts",
            "benchmarks"));

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
}
