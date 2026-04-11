using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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

    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "--profile-svg", StringComparison.Ordinal))
        {
            return false;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: --profile-svg <path> [iterations]");
            return true;
        }

        var path = args[1];
        var iterations = args.Length >= 3 && int.TryParse(args[2], out var parsedIterations) && parsedIterations > 0
            ? parsedIterations
            : DefaultIterations;

        Run(path, iterations, DefaultWarmupIterations);
        return true;
    }

    private static void Run(string path, int iterations, int warmupIterations)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("SVG file not found.", path);
        }

        var svgText = File.ReadAllText(path);
        var fileInfo = new FileInfo(path);
        var stageSkiaModel = new SkiaModel(new SKSvgSettings());
        var stageAssetLoader = new SkiaSvgAssetLoader(stageSkiaModel);
        var parsedDocument = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svgText);
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

        Console.WriteLine($"SVG: {path}");
        Console.WriteLine($"Size: {fileInfo.Length} bytes");
        Console.WriteLine($"Iterations: {iterations} (warmup {warmupIterations})");
        Console.WriteLine();

        Warmup(svgText, warmupIterations);

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
                using var picture = svg.Load(stream);
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
                var changedElement = MutateFirstVisualFill(svg.SourceDocument);
                SvgSceneMutationResult? result = null;
                var updated = changedElement is not null &&
                              svg.TryApplyRetainedSceneMutationAndRender(changedElement, new[] { "fill" }, out result);
                return ProfileSample.From(scope, updated ? result?.CompilationRootCount ?? 0 : -1);
            })
        };

        PrintSummary(results);
    }

    private static void Warmup(string svgText, int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            using var svg = new SKSvg();
            svg.FromSvg(svgText);
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
