using System;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Svg.Skia.Benchmarks;

public class SvgLayerBoundsBenchmarks
{
    private SKSvg? retainedSvg;

    [Params(32, 128, 256)]
    public int WrapperCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        retainedSvg = new SKSvg();
        if (retainedSvg.FromSvg(CreateOpacityWrapperSvg(WrapperCount)) is null)
        {
            throw new InvalidOperationException($"Failed to load generated opacity-wrapper SVG with {WrapperCount} wrappers.");
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        retainedSvg?.Dispose();
    }

    [Benchmark]
    [BenchmarkCategory("NativePicture", "LayerBounds", "OpacityWrappers")]
    public float CreateRetainedSceneGraphPicture()
    {
        using var picture = retainedSvg!.CreateRetainedSceneGraphPicture();
        return picture?.CullRect.Width ?? 0f;
    }

    private static string CreateOpacityWrapperSvg(int wrapperCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">""");
        for (var i = 0; i < wrapperCount; i++)
        {
            builder.Append("<g opacity=\"");
            builder.Append((0.9 - ((i % 5) * 0.05)).ToString("0.##", CultureInfo.InvariantCulture));
            builder.AppendLine("\">");
        }

        builder.AppendLine("""  <rect x="24" y="24" width="208" height="208" fill="#0ea5e9" />""");
        builder.AppendLine("""  <rect x="48" y="48" width="160" height="160" fill="#111827" opacity="0.35" />""");

        for (var i = 0; i < wrapperCount; i++)
        {
            builder.AppendLine("</g>");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }
}
