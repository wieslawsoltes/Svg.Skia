using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using SkiaSharp;

namespace Svg.Skia.Benchmarks;

[MemoryDiagnoser]
public class SvgAnimationFrameBenchmarks
{
    private static readonly string[] Palette =
    {
        "crimson",
        "royalblue",
        "seagreen",
        "darkorange",
        "mediumvioletred",
        "slateblue"
    };

    private static readonly TimeSpan[] FrameTimes = CreateFrameTimes();

    private SKSvg? _layeredAdvanceSvg;
    private SKSvg? _layeredDrawSvg;
    private SKSvg? _resourceAdvanceSvg;
    private SKSvg? _resourceDrawSvg;
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private int _layeredAdvanceFrameIndex;
    private int _layeredDrawFrameIndex;
    private int _resourceAdvanceFrameIndex;
    private int _resourceDrawFrameIndex;

    [Params(64, 256)]
    public int StaticElementCount { get; set; }

    [Params(4, 16)]
    public int AnimatedElementCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var layeredSvg = BuildLayeredSvg(StaticElementCount, AnimatedElementCount);
        var resourceSvg = BuildPaintServerFallbackSvg(StaticElementCount, AnimatedElementCount);

        _layeredAdvanceSvg = CreateSvg(layeredSvg, shouldUseLayerCaching: true);
        _layeredDrawSvg = CreateSvg(layeredSvg, shouldUseLayerCaching: true);
        _resourceAdvanceSvg = CreateSvg(resourceSvg, shouldUseLayerCaching: true);
        _resourceDrawSvg = CreateSvg(resourceSvg, shouldUseLayerCaching: true);

        _bitmap = new SKBitmap(512, 512);
        _canvas = new SKCanvas(_bitmap);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _canvas?.Dispose();
        _bitmap?.Dispose();
        _layeredAdvanceSvg?.Dispose();
        _layeredDrawSvg?.Dispose();
        _resourceAdvanceSvg?.Dispose();
        _resourceDrawSvg?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int AdvanceLayeredFrame()
    {
        return AdvanceFrame(_layeredAdvanceSvg!, ref _layeredAdvanceFrameIndex);
    }

    [Benchmark]
    public int AdvanceResourceFrame()
    {
        return AdvanceFrame(_resourceAdvanceSvg!, ref _resourceAdvanceFrameIndex);
    }

    [Benchmark]
    public int AdvanceAndDrawLayeredFrame()
    {
        return AdvanceFrameAndDraw(_layeredDrawSvg!, ref _layeredDrawFrameIndex);
    }

    [Benchmark]
    public int AdvanceAndDrawResourceFrame()
    {
        return AdvanceFrameAndDraw(_resourceDrawSvg!, ref _resourceDrawFrameIndex);
    }

    private int AdvanceFrame(SKSvg svg, ref int frameIndex)
    {
        svg.SetAnimationTime(NextFrameTime(ref frameIndex));
        return svg.LastAnimationDirtyTargetCount;
    }

    private int AdvanceFrameAndDraw(SKSvg svg, ref int frameIndex)
    {
        svg.SetAnimationTime(NextFrameTime(ref frameIndex));
        _canvas!.Clear(SKColors.Transparent);
        svg.Draw(_canvas);
        return svg.LastAnimationDirtyTargetCount;
    }

    private static SKSvg CreateSvg(string svgText, bool shouldUseLayerCaching)
    {
        var svg = new SKSvg();
        svg.FromSvg(svgText);

        if (svg.UsesAnimationLayerCaching != shouldUseLayerCaching)
        {
            svg.Dispose();
            throw new InvalidOperationException(
                shouldUseLayerCaching
                    ? "The layered benchmark document did not enable animation layer caching."
                    : "The resource-backed benchmark document did not enable animation layer caching.");
        }

        return svg;
    }

    private static TimeSpan NextFrameTime(ref int frameIndex)
    {
        var time = FrameTimes[frameIndex % FrameTimes.Length];
        frameIndex++;
        return time;
    }

    private static TimeSpan[] CreateFrameTimes()
    {
        var times = new TimeSpan[120];
        for (var i = 0; i < times.Length; i++)
        {
            times[i] = TimeSpan.FromMilliseconds(i * (2000.0 / times.Length));
        }

        return times;
    }

    private static string BuildLayeredSvg(int staticElementCount, int animatedElementCount)
    {
        const int width = 320;
        var height = CalculateSceneHeight(staticElementCount, animatedElementCount);
        var builder = CreateSvgBuilder(width, height);

        AppendStaticElements(builder, staticElementCount);

        for (var i = 0; i < animatedElementCount; i++)
        {
            var y = 120 + (i * 12);
            var color = Palette[i % Palette.Length];
            builder.AppendLine($"""  <g id="animated-root-{i}">""");
            builder.AppendLine($"""    <rect id="animated-{i}" x="0" y="{y}" width="18" height="8" fill="{color}">""");
            builder.AppendLine("""      <animate attributeName="x" from="0" to="220" dur="2s" repeatCount="indefinite" />""");
            builder.AppendLine("""    </rect>""");
            builder.AppendLine("""  </g>""");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildPaintServerFallbackSvg(int staticElementCount, int animatedElementCount)
    {
        const int width = 320;
        var height = CalculateSceneHeight(staticElementCount, animatedElementCount);
        var builder = CreateSvgBuilder(width, height);

        builder.AppendLine("  <defs>");
        for (var i = 0; i < animatedElementCount; i++)
        {
            var y = 120 + (i * 12);
            var color = Palette[i % Palette.Length];
            var gradientId = $"gradient-{i}";
            builder.AppendLine($"""    <linearGradient id="{gradientId}" x1="0%" y1="0%" x2="100%" y2="0%">""");
            builder.AppendLine($"""      <stop id="gradient-stop-{i}" offset="0%" stop-color="{color}">""");
            builder.AppendLine("""        <animate attributeName="stop-color" values="crimson;royalblue;seagreen;darkorange" dur="2s" repeatCount="indefinite" />""");
            builder.AppendLine("""      </stop>""");
            builder.AppendLine("""      <stop offset="100%" stop-color="white" />""");
            builder.AppendLine("""    </linearGradient>""");
        }

        builder.AppendLine("  </defs>");
        AppendStaticElements(builder, staticElementCount);

        for (var i = 0; i < animatedElementCount; i++)
        {
            var y = 120 + (i * 12);
            builder.AppendLine($"""  <rect id="gradient-target-{i}" x="0" y="{y}" width="220" height="8" fill="url(#gradient-{i})" />""");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static void AppendStaticElements(StringBuilder builder, int staticElementCount)
    {
        const int columns = 8;
        for (var i = 0; i < staticElementCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = 8 + (column * 28);
            var y = 8 + (row * 10);
            var color = Palette[i % Palette.Length];
            builder.AppendLine($"""  <rect id="static-{i}" x="{x}" y="{y}" width="20" height="6" fill="{color}" opacity="0.8" />""");
        }
    }

    private static StringBuilder CreateSvgBuilder(int width, int height, bool includeXLink = false)
    {
        var builder = new StringBuilder();
        builder.Append($"""<svg xmlns="http://www.w3.org/2000/svg" """);
        if (includeXLink)
        {
            builder.Append("""xmlns:xlink="http://www.w3.org/1999/xlink" """);
        }

        builder.AppendLine($"""width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        return builder;
    }

    private static int CalculateSceneHeight(int staticElementCount, int animatedElementCount)
    {
        var staticRows = Math.Max(1, (int)Math.Ceiling(staticElementCount / 8d));
        return 140 + (staticRows * 10) + (animatedElementCount * 12);
    }
}
