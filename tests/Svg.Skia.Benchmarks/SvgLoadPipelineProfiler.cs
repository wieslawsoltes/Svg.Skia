using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia.Benchmarks;

internal static class SvgLoadPipelineProfiler
{
    private const int DefaultIterations = 12;
    private const int DefaultWarmupIterations = 3;
    private const string ProfileOutputEnvironmentVariable = "SVG_SKIA_PROFILE_OUTPUT";

    public static bool TryRun(string[] args, string artifactsPath)
    {
        if (args.Length == 0 || !string.Equals(args[0], "--profile-svg", StringComparison.Ordinal))
        {
            return false;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: --profile-svg <path> [iterations] [--profile-output <path>]");
            return true;
        }

        var path = args[1];
        var iterations = DefaultIterations;
        string? outputPath = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--profile-output", StringComparison.Ordinal))
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Usage: --profile-svg <path> [iterations] [--profile-output <path>]");
                    return true;
                }

                outputPath = args[++i];
                continue;
            }

            if (int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedIterations) &&
                parsedIterations > 0)
            {
                iterations = parsedIterations;
                continue;
            }

            Console.Error.WriteLine($"Unknown profile argument: {args[i]}");
            Console.Error.WriteLine("Usage: --profile-svg <path> [iterations] [--profile-output <path>]");
            return true;
        }

        outputPath ??= Environment.GetEnvironmentVariable(ProfileOutputEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = ResolveDefaultProfileOutputPath(artifactsPath, path);
        }
        else
        {
            outputPath = Path.GetFullPath(outputPath);
        }

        Run(path, iterations, DefaultWarmupIterations, outputPath);
        return true;
    }

    private static void Run(string path, int iterations, int warmupIterations, string outputPath)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("SVG file not found.", path);
        }

        var absolutePath = Path.GetFullPath(path);
        var fileUri = new Uri(absolutePath);
        var svgText = File.ReadAllText(absolutePath);
        var fileInfo = new FileInfo(absolutePath);
        var stageSkiaModel = new SkiaModel(new SKSvgSettings());
        var stageAssetLoader = new SkiaSvgAssetLoader(stageSkiaModel);
        var parsedDocument = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svgText);
        parsedDocument.BaseUri = fileUri;
        if (!SvgSceneRuntime.TryCompile(parsedDocument, stageAssetLoader, DrawAttributes.None, out var compiledScene) ||
            compiledScene is null)
        {
            throw new InvalidOperationException("Failed to compile the SVG scene for profiling.");
        }

        var shimModel = compiledScene.CreateModel();
        if (shimModel is null)
        {
            throw new InvalidOperationException("Failed to create the shim SKPicture model for profiling.");
        }

        using var nativePicture = stageSkiaModel.ToSKPicture(shimModel);
        if (nativePicture is null)
        {
            throw new InvalidOperationException("Failed to create the native SkiaSharp.SKPicture for profiling.");
        }

        Console.WriteLine($"SVG: {absolutePath}");
        Console.WriteLine($"Size: {fileInfo.Length} bytes");
        Console.WriteLine($"Iterations: {iterations} (warmup {warmupIterations})");
        Console.WriteLine();

        Warmup(svgText, fileUri, warmupIterations);

        var results = new List<ProfileResult>
        {
            Measure("Parse SvgDocument from string", iterations, () =>
            {
                using var scope = ThreadAllocationScope.Start();
                var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svgText);
                return ProfileSample.From(scope, document?.Children.Count ?? 0);
            }),
            Measure("Compile retained scene (parsed doc)", iterations, () =>
            {
                using var scope = ThreadAllocationScope.Start();
                var succeeded = SvgSceneRuntime.TryCompile(parsedDocument, stageAssetLoader, DrawAttributes.None, out var sceneDocument);
                return ProfileSample.From(scope, succeeded && sceneDocument is not null ? sceneDocument.Root.Children.Count : 0);
            }),
            Measure("Create shim picture model", iterations, () =>
            {
                using var scope = ThreadAllocationScope.Start();
                var model = compiledScene.CreateModel();
                return ProfileSample.From(scope, model?.Commands?.Count ?? 0);
            }),
            Measure("Create native SKPicture", iterations, () =>
            {
                var skiaModel = new SkiaModel(new SKSvgSettings());
                using var scope = ThreadAllocationScope.Start();
                using var picture = skiaModel.ToSKPicture(shimModel);
                return ProfileSample.From(scope, picture?.CullRect.Width ?? 0f);
            }),
            Measure("Render native picture to bitmap", iterations, () =>
            {
                using var scope = ThreadAllocationScope.Start();
                using var bitmap = nativePicture.ToBitmap(SKColors.Transparent, 1f, 1f, SKColorType.Rgba8888, SKAlphaType.Premul, stageSkiaModel.Settings.Srgb);
                return ProfileSample.From(scope, bitmap?.ByteCount ?? 0);
            }),
            Measure("Encode native picture to PNG", iterations, () =>
            {
                using var scope = ThreadAllocationScope.Start();
                using var stream = new MemoryStream();
                var saved = nativePicture.ToImage(stream, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1f, 1f, SKColorType.Rgba8888, SKAlphaType.Premul, stageSkiaModel.Settings.Srgb);
                return ProfileSample.From(scope, saved ? stream.Length : 0);
            }),
            Measure("Load via SKSvg.FromSvg", iterations, () =>
            {
                using var scope = ThreadAllocationScope.Start();
                using var svg = new SKSvg();
                using var picture = svg.FromSvg(svgText);
                return ProfileSample.From(scope, picture?.CullRect.Width ?? 0f);
            }),
            Measure("Control-like source load", iterations, () =>
            {
                using var scope = ThreadAllocationScope.Start();
                var bytes = Encoding.UTF8.GetBytes(svgText);
                using var stream = new MemoryStream(bytes);
                using var svg = new SKSvg();
                using var picture = svg.Load(stream, parameters: null, baseUri: fileUri);
                return ProfileSample.From(scope, picture?.CullRect.Height ?? 0f);
            }),
            Measure("Mutate + full FromSvgDocument rebuild", iterations, () =>
            {
                using var scope = ThreadAllocationScope.Start();
                using var svg = new SKSvg();
                svg.FromSvg(svgText);
                MutateFirstVisualFill(svg.SourceDocument);
                using var picture = svg.FromSvgDocument(svg.SourceDocument);
                return ProfileSample.From(scope, picture?.CullRect.Width ?? 0f);
            }),
            Measure("Mutate + retained scene rebuild", iterations, () =>
            {
                using var scope = ThreadAllocationScope.Start();
                using var svg = new SKSvg();
                svg.FromSvg(svgText);
                var changedElement = MutateFirstVisualFill(svg.RetainedSceneGraph?.SourceDocument);
                SvgSceneMutationResult? result = null;
                var updated = changedElement is not null &&
                              svg.TryApplyRetainedSceneMutationAndRender(changedElement, new[] { "fill" }, out result);
                return ProfileSample.From(scope, updated ? result?.CompilationRootCount ?? 0 : -1);
            })
        };

        PrintSummary(results);
        WriteMarkdownReport(outputPath, absolutePath, fileInfo.Length, iterations, warmupIterations, results);
        Console.WriteLine();
        Console.WriteLine($"Profile report: {outputPath}");
    }

    private static void Warmup(string svgText, Uri baseUri, int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var bytes = Encoding.UTF8.GetBytes(svgText);
            using var stream = new MemoryStream(bytes);
            using var svg = new SKSvg();
            svg.Load(stream, parameters: null, baseUri: baseUri);
            using var bitmap = svg.Picture?.ToBitmap(SKColors.Transparent, 1f, 1f, SKColorType.Rgba8888, SKAlphaType.Premul, svg.Settings.Srgb);
        }
    }

    private static ProfileResult Measure(string name, int iterations, Func<ProfileSample> sampleFactory)
    {
        var elapsedSamples = new double[iterations];
        var allocationSamples = new long[iterations];
        var payloadSamples = new double[iterations];

        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var sample = sampleFactory();
            stopwatch.Stop();
            elapsedSamples[i] = stopwatch.Elapsed.TotalMilliseconds;
            allocationSamples[i] = sample.AllocatedBytes;
            payloadSamples[i] = sample.Payload;
        }

        return new ProfileResult(
            name,
            elapsedSamples.Average(),
            Percentile(elapsedSamples, 0.95),
            allocationSamples.Average(),
            payloadSamples.Average());
    }

    private static double Percentile(double[] samples, double percentile)
    {
        var ordered = samples.OrderBy(x => x).ToArray();
        if (ordered.Length == 0)
        {
            return 0d;
        }

        var index = (int)Math.Ceiling((ordered.Length - 1) * percentile);
        return ordered[index];
    }

    private static SvgVisualElement? MutateFirstVisualFill(SvgDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        var element = document.Descendants().OfType<SvgVisualElement>().FirstOrDefault(static visual => visual.Fill is not null);
        if (element is null)
        {
            return null;
        }

        element.Fill = new SvgColourServer(Color.BlueViolet);
        return element;
    }

    private static void PrintSummary(IReadOnlyList<ProfileResult> results)
    {
        Console.WriteLine("Stage timings");
        Console.WriteLine("-------------");
        foreach (var result in results)
        {
            Console.WriteLine(
                $"{result.Name,-36} mean {result.MeanMilliseconds,8:F2} ms  p95 {result.P95Milliseconds,8:F2} ms  alloc {result.MeanAllocatedBytes / 1024d / 1024d,8:F2} MB");
        }
    }

    private static string ResolveDefaultProfileOutputPath(string artifactsPath, string svgPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(svgPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "svg";
        }

        return Path.GetFullPath(Path.Combine(artifactsPath, "profiles", $"{fileName}.profile.md"));
    }

    private static void WriteMarkdownReport(
        string outputPath,
        string svgPath,
        long svgBytes,
        int iterations,
        int warmupIterations,
        IReadOnlyList<ProfileResult> results)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("# Svg.Skia Load Pipeline Profile");
        builder.AppendLine();
        builder.AppendLine($"- SVG: `{svgPath}`");
        builder.AppendLine(FormattableString.Invariant($"- Size: `{svgBytes}` bytes"));
        builder.AppendLine(FormattableString.Invariant($"- Iterations: `{iterations}`"));
        builder.AppendLine(FormattableString.Invariant($"- Warmup iterations: `{warmupIterations}`"));
        builder.AppendLine(FormattableString.Invariant($"- Generated UTC: `{DateTimeOffset.UtcNow:O}`"));
        builder.AppendLine();
        builder.AppendLine("| Stage | Mean (ms) | P95 (ms) | Mean alloc (MB) | Payload |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: |");

        foreach (var result in results)
        {
            builder.Append("| ");
            builder.Append(result.Name);
            builder.Append(" | ");
            builder.Append(result.MeanMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
            builder.Append(" | ");
            builder.Append(result.P95Milliseconds.ToString("F2", CultureInfo.InvariantCulture));
            builder.Append(" | ");
            builder.Append((result.MeanAllocatedBytes / 1024d / 1024d).ToString("F2", CultureInfo.InvariantCulture));
            builder.Append(" | ");
            builder.Append(result.MeanPayload.ToString("F2", CultureInfo.InvariantCulture));
            builder.AppendLine(" |");
        }

        File.WriteAllText(outputPath, builder.ToString());
    }

    private readonly record struct ProfileSample(long AllocatedBytes, double Payload)
    {
        public static ProfileSample From(ThreadAllocationScope scope, double payload)
            => new(scope.GetAllocatedBytes(), payload);
    }

    private readonly record struct ProfileResult(
        string Name,
        double MeanMilliseconds,
        double P95Milliseconds,
        double MeanAllocatedBytes,
        double MeanPayload);

    private struct ThreadAllocationScope : IDisposable
    {
        private readonly long _startBytes;

        private ThreadAllocationScope(long startBytes)
        {
            _startBytes = startBytes;
        }

        public static ThreadAllocationScope Start()
        {
            return new ThreadAllocationScope(GC.GetAllocatedBytesForCurrentThread());
        }

        public long GetAllocatedBytes()
        {
            return GC.GetAllocatedBytesForCurrentThread() - _startBytes;
        }

        public void Dispose()
        {
        }
    }
}
