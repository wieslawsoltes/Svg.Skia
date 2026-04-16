using System;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using SkiaSharp;

namespace Svg.Skia.Benchmarks;

public class SvgSingularLayerBoundsBenchmarks
{
    private SKSvg? retainedSvg;
    private SKPicture? nativePicture;
    private SKBitmap? reusableBitmap;
    private SKCanvas? reusableCanvas;

    [Params(32, 128, 256)]
    public int WrapperCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        retainedSvg = new SKSvg();
        if (retainedSvg.FromSvg(CreateSingularOpacityWrapperSvg(WrapperCount)) is null)
        {
            throw new InvalidOperationException($"Failed to load generated singular opacity-wrapper SVG with {WrapperCount} wrappers.");
        }

        nativePicture = retainedSvg.Picture ?? throw new InvalidOperationException($"Failed to create native picture for singular opacity-wrapper SVG with {WrapperCount} wrappers.");
        reusableBitmap = new SKBitmap(new SKImageInfo(256, 256, SKColorType.Rgba8888, SKAlphaType.Premul));
        reusableCanvas = new SKCanvas(reusableBitmap);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        reusableCanvas?.Dispose();
        reusableBitmap?.Dispose();
        nativePicture?.Dispose();
        retainedSvg?.Dispose();
    }

    [Benchmark]
    [BenchmarkCategory("Render", "LayerBounds", "SingularTransform", "OpacityWrappers")]
    public int DrawNativePicture1x()
    {
        reusableCanvas!.Clear(SKColors.Transparent);
        reusableCanvas.DrawPicture(nativePicture!);
        return reusableBitmap!.ByteCount;
    }

    private static string CreateSingularOpacityWrapperSvg(int wrapperCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">""");
        builder.AppendLine("""  <g transform="matrix(0 0 0 1 128 0)">""");

        for (var i = 0; i < wrapperCount; i++)
        {
            builder.Append("    <g opacity=\"");
            builder.Append((0.92 - ((i % 5) * 0.04)).ToString("0.##", CultureInfo.InvariantCulture));
            builder.AppendLine("\">");
        }

        builder.AppendLine("""      <rect x="24" y="24" width="208" height="208" fill="none" stroke="#0ea5e9" stroke-width="12" />""");
        builder.AppendLine("""      <path d="M40 32 L216 224 M216 32 L40 224" fill="none" stroke="#111827" stroke-width="10" stroke-linecap="round" />""");

        for (var i = 0; i < wrapperCount; i++)
        {
            builder.AppendLine("    </g>");
        }

        builder.AppendLine("  </g>");
        builder.AppendLine("</svg>");
        return builder.ToString();
    }
}
