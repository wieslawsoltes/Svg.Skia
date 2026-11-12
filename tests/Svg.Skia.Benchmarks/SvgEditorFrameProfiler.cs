using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Svg.Editor.Skia;

namespace Svg.Skia.Benchmarks;

/// <summary>Deterministic in-process measurements of editor repaint preparation.</summary>
internal static class SvgEditorFrameProfiler
{
    private const int Iterations = 2048;
    private const int Samples = 7;
    private const int WarmupIterations = 128;

    private sealed record Measurement(double MedianMicrosecondsPerIteration, double AllocatedBytesPerIteration,
        int IterationsPerSample, int Samples, double Checksum);
    private sealed record Comparison(string Name, Measurement Uncached, Measurement Cached);

    public static bool TryRun(string[] args, string artifactsPath)
    {
        if (args.Length == 0 || args[0] != "--profile-editor-frames")
            return false;
        var output = Path.Combine(artifactsPath, "editor-frame-profile.json");
        if (args.Length == 3 && args[1] == "--profile-output")
            output = Path.GetFullPath(args[2]);
        else if (args.Length != 1)
            throw new ArgumentException("Usage: --profile-editor-frames [--profile-output <path>]");

        var comparisons = new List<Comparison>();
        foreach (var named in new[] { false, true })
        {
            using var editor = new SvgEditorController();
            editor.LoadSvg(CreateDocument(named));
            var cache = new SvgEditorArtboardCache();
            comparisons.Add(Compare(named ? "128 named artboards / 1000 artwork nodes" : "Implicit artboard / 10000 artwork nodes",
                () => ConsumeArtboards(editor.GetArtboards()), () => ConsumeArtboards(cache.GetArtboards(editor))));
        }

        var ruler = new SvgRulerTickCache();
        comparisons.Add(Compare("Stationary ruler / 1200 DIP / pointer-only frames",
            () => ConsumeTicks(SvgRulerMetrics.Create(137, 1, 1200)),
            () => ConsumeTicks(ruler.GetTicks(137, 1, 1200))));

        var report = new
        {
            Scope = "Steady-state editor repaint preparation. Excludes document loading, SVG rasterization and GPU composition.",
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            WarmupIterations,
            Comparisons = comparisons
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(output, json);
        Console.WriteLine(json);
        Console.WriteLine($"Editor frame profile: {output}");
        return true;
    }

    private static Comparison Compare(string name, Func<double> uncached, Func<double> cached)
    {
        var before = Measure(uncached);
        var after = Measure(cached);
        if (before.Checksum != after.Checksum)
            throw new InvalidOperationException($"Cached geometry differs from live geometry: {name}");
        return new Comparison(name, before, after);
    }

    private static Measurement Measure(Func<double> action)
    {
        double checksum = 0;
        for (var index = 0; index < WarmupIterations; index++)
            checksum += action();
        var elapsed = new double[Samples];
        var allocated = new double[Samples];
        for (var sample = 0; sample < Samples; sample++)
        {
            var bytes = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var index = 0; index < Iterations; index++)
                checksum += action();
            elapsed[sample] = (Stopwatch.GetTimestamp() - started) * 1_000_000d / Stopwatch.Frequency / Iterations;
            allocated[sample] = (GC.GetAllocatedBytesForCurrentThread() - bytes) / (double)Iterations;
        }
        Array.Sort(elapsed);
        Array.Sort(allocated);
        return new Measurement(elapsed[Samples / 2], allocated[Samples / 2], Iterations, Samples, checksum);
    }

    private static double ConsumeArtboards(IReadOnlyList<SvgEditorArtboard> boards)
    {
        double result = boards.Count;
        for (var index = 0; index < boards.Count; index++)
        {
            var bounds = boards[index].Bounds;
            result += bounds.Left + bounds.Top + bounds.Right + bounds.Bottom;
        }
        return result;
    }

    private static double ConsumeTicks(IReadOnlyList<SvgRulerTick> ticks)
    {
        double result = ticks.Count;
        // This is the work performed by a ruler paint: minor ticks have no label.
        for (var index = 0; index < ticks.Count; index++)
        {
            result += ticks[index].Pixel + ticks[index].Value;
            if (ticks[index].IsMajor)
                result += ticks[index].Label.Length;
        }
        return result;
    }

    private static string CreateDocument(bool named)
    {
        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1200' height='800'>");
        var nodes = named ? 1000 : 10000;
        for (var index = 0; index < nodes; index++)
            source.Append("<rect x='").Append(index % 1200).Append("' y='").Append(index % 800).Append("' width='1' height='1'/>");
        if (named)
            for (var index = 0; index < 128; index++)
                source.Append("<view id='board-").Append(index).Append("' data-unodraw-artboard='true' data-unodraw-name='Board ")
                    .Append(index).Append("' viewBox='0 0 1200 800'/>");
        return source.Append("</svg>").ToString();
    }
}
