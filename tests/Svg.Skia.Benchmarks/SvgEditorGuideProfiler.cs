using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using SkiaSharp;
using Svg.Editor.Skia;

namespace Svg.Skia.Benchmarks;

/// <summary>Bounded, equivalent-result measurements of the selection-drag snap query.</summary>
internal static class SvgEditorGuideProfiler
{
    private const int Iterations = 256;
    private const int Samples = 5;
    private const int WarmupIterations = 128;
    private sealed record Measurement(double MedianMicrosecondsPerIteration, double AllocatedBytesPerIteration);
    private sealed record Comparison(string Name, int Guides, Measurement Legacy, Measurement Indexed, double Speedup, double Checksum);

    public static bool TryRun(string[] args, string artifactsPath)
    {
        if (args.Length == 0 || args[0] != "--profile-editor-guides")
            return false;
        var output = Path.Combine(artifactsPath, "editor-guide-profile.json");
        if (args.Length == 3 && args[1] == "--profile-output")
            output = Path.GetFullPath(args[2]);
        else if (args.Length != 1)
            throw new ArgumentException("Usage: --profile-editor-guides [--profile-output <path>]");

        var comparisons = new List<Comparison>();
        foreach (var (objects, diagonal) in new[] { (100, 0), (1000, 0), (1000, 128) })
        {
            using var editor = new SvgEditorController();
            editor.LoadSvg(CreateDocument(objects));
            if (diagonal > 0)
            {
                var guides = new List<SvgEditorGuide>();
                for (var index = 0; index < diagonal; index++)
                    guides.Add(new SvgEditorGuide(new Guid(index, 0, 0, new byte[8]).ToString("N"),
                        new SKPoint(index * 9, index * 3), new SKPoint(3, 4)));
                SvgEditorGuideService.Write(editor.Document!, guides);
                editor.Refresh();
            }
            var cache = new SvgEditorGuideCache();
            var indexed = cache.GetSnapIndex(editor, true, true);
            var candidates = indexed.Guides;
            var legacyCandidates = new List<SvgEditorGuide>(candidates.Count);
            var points = CreatePoints();
            foreach (var point in points)
                for (var column = 0; column < 3; column++)
                    for (var row = 0; row < 3; row++)
                    {
                        var anchor = new SKPoint(point.X + column * 6, point.Y + row * 6);
                        var expected = SvgEditorGuideService.Snap(anchor, 5, candidates, out var first, out var second);
                        var actual = indexed.Snap(anchor, 5, out var indexedFirst, out var indexedSecond);
                        if (expected != actual || first != indexedFirst || second != indexedSecond)
                            throw new InvalidOperationException("Indexed snapping changed a measured point or guide choice.");
                    }

            double Legacy(int iteration)
            {
                var point = points[iteration % points.Length];
                legacyCandidates.Clear();
                legacyCandidates.AddRange(candidates);
                double checksum = 0;
                foreach (var x in new[] { point.X, point.X + 6, point.X + 12 })
                    foreach (var y in new[] { point.Y, point.Y + 6, point.Y + 12 })
                    {
                        var snapped = SvgEditorGuideService.Snap(new SKPoint(x, y), 5, legacyCandidates, out var first, out var second);
                        checksum += snapped.X + snapped.Y + (first?.Length ?? 0) + (second?.Length ?? 0);
                    }
                return checksum;
            }

            double Indexed(int iteration)
            {
                var point = points[iteration % points.Length];
                var current = cache.GetSnapIndex(editor, true, true);
                double checksum = 0;
                for (var column = 0; column < 3; column++)
                    for (var row = 0; row < 3; row++)
                    {
                        var snapped = current.Snap(new SKPoint(point.X + column * 6, point.Y + row * 6), 5, out var first, out var second);
                        checksum += snapped.X + snapped.Y + (first?.Length ?? 0) + (second?.Length ?? 0);
                    }
                return checksum;
            }

            for (var iteration = 0; iteration < WarmupIterations; iteration++)
            { Legacy(iteration); Indexed(iteration); }
            var legacyElapsed = new double[Samples];
            var indexedElapsed = new double[Samples];
            var legacyAllocated = new double[Samples];
            var indexedAllocated = new double[Samples];
            double checksum = 0;
            for (var sample = 0; sample < Samples; sample++)
            {
                // Alternate order so every case is not consistently measured hot or cold.
                var first = Measure(sample % 2 == 0 ? Legacy : Indexed);
                var second = Measure(sample % 2 == 0 ? Indexed : Legacy);
                var legacy = sample % 2 == 0 ? first : second;
                var optimized = sample % 2 == 0 ? second : first;
                if (legacy.Checksum != optimized.Checksum)
                    throw new InvalidOperationException("Indexed snapping changed the measured checksum.");
                checksum += legacy.Checksum;
                legacyElapsed[sample] = legacy.Elapsed;
                indexedElapsed[sample] = optimized.Elapsed;
                legacyAllocated[sample] = legacy.Allocated;
                indexedAllocated[sample] = optimized.Allocated;
            }
            Array.Sort(legacyElapsed);
            Array.Sort(indexedElapsed);
            Array.Sort(legacyAllocated);
            Array.Sort(indexedAllocated);
            var before = new Measurement(legacyElapsed[Samples / 2], legacyAllocated[Samples / 2]);
            var after = new Measurement(indexedElapsed[Samples / 2], indexedAllocated[Samples / 2]);
            comparisons.Add(new Comparison($"{objects} artwork objects / {diagonal} diagonal guides / nine drag anchors", candidates.Count,
                before, after, before.MedianMicrosecondsPerIteration / after.MedianMicrosecondsPerIteration, checksum));
        }
        var report = new
        {
            Scope = "Steady-state selection-drag snapping: nine anchors, cached candidate construction, unchanged 5-unit tolerance and guide limit. Excludes SVG rendering, texture upload and GPU composition.",
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Iterations,
            Samples,
            WarmupIterations,
            Comparisons = comparisons
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(output, json);
        Console.WriteLine(json);
        Console.WriteLine($"Editor guide profile: {output}");
        return true;
    }

    private static (double Elapsed, double Allocated, double Checksum) Measure(Func<int, double> action)
    {
        double checksum = 0;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < Iterations; iteration++)
            checksum += action(iteration);
        return ((Stopwatch.GetTimestamp() - started) * 1_000_000d / Stopwatch.Frequency / Iterations,
            (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)Iterations, checksum);
    }

    private static SKPoint[] CreatePoints()
    {
        var result = new SKPoint[128];
        for (var index = 0; index < result.Length; index++)
            result[index] = new SKPoint(37 + index * 7.17f, 36 + (float)Math.Sin(index * 0.17) * 29);
        return result;
    }

    private static string CreateDocument(int count)
    {
        var result = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1200' height='800'>");
        for (var index = 0; index < count; index++)
            result.Append("<rect x='").Append(index % 50 * 24).Append("' y='").Append(index / 50 * 24).Append("' width='12' height='12'/>");
        return result.Append("</svg>").ToString();
    }
}
