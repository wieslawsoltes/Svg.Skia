using System.IO;
using SkiaSharp;
using Xunit;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaColorType = SkiaSharp.SKColorType;

namespace Svg.Skia.UnitTests;

public class StructFragmentRenderingTests
{
    [Fact]
    public void RootSvgWithPercentageSizeAndNoViewBox_UsesRenderableBounds()
    {
        var svgPath = Path.Combine(
            "..",
            "..",
            "..",
            "..",
            "..",
            "externals",
            "W3C_SVG_11_TestSuite",
            "W3C_SVG_11_TestSuite",
            "svg",
            "struct-frag-04-t.svg");

        using var svg = new SKSvg();
        svg.Settings.StandaloneViewport = SKRect.Create(0f, 0f, 480f, 360f);
        using var _ = svg.Load(svgPath);

        Assert.NotNull(svg.RetainedSceneGraph);
        Assert.Equal(480f, svg.RetainedSceneGraph!.CullRect.Width);
        Assert.Equal(360f, svg.RetainedSceneGraph.CullRect.Height);

        Assert.NotNull(svg.Picture);
        Assert.Equal(480f, svg.Picture!.CullRect.Width);
        Assert.Equal(360f, svg.Picture.CullRect.Height);

        using var bitmap = svg.Picture.ToBitmap(
            SKColors.White,
            scaleX: 1f,
            scaleY: 1f,
            SkiaColorType.Rgba8888,
            SkiaAlphaType.Unpremul,
            svg.Settings.Srgb);

        Assert.NotNull(bitmap);

        var background = bitmap!.GetPixel(260, 180);
        var crimson = bitmap.GetPixel(110, 110);
        var gold = bitmap.GetPixel(175, 125);

        Assert.True(background.Red > 240 && background.Green > 240 && background.Blue > 240);
        Assert.True(crimson.Red > 180 && crimson.Green < 80 && crimson.Blue < 120);
        Assert.True(gold.Red > 200 && gold.Green > 150 && gold.Blue < 80);
    }
}
