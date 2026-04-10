using ShimSkiaSharp;
using Xunit;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaColors = SkiaSharp.SKColors;
using SkiaColorType = SkiaSharp.SKColorType;

namespace Svg.Skia.UnitTests;

public class OpacityRenderingTests
{
    [Fact]
    public void RootSvgWithoutExplicitWidth_UsesRenderableBoundsAndPreservesOpacitySemantics()
    {
        using var svg = new SKSvg();
        svg.FromSvg(OpacitySvg);

        Assert.NotNull(svg.RetainedSceneGraph);
        Assert.True(svg.RetainedSceneGraph!.CullRect.Width > 0f);
        Assert.True(svg.RetainedSceneGraph.CullRect.Height > 0f);

        Assert.NotNull(svg.Picture);
        Assert.True(svg.Picture!.CullRect.Width > 0f);
        Assert.True(svg.Picture.CullRect.Height > 0f);

        using var bitmap = svg.Picture.ToBitmap(
            SkiaColors.Transparent,
            1f,
            1f,
            SkiaColorType.Rgba8888,
            SkiaAlphaType.Unpremul,
            svg.Settings.Srgb);

        Assert.NotNull(bitmap);
        AssertOpacitySemantics(bitmap!);
    }

    private static void AssertOpacitySemantics(SkiaBitmap bitmap)
    {
        var topLeftSingle = bitmap.GetPixel(15, 15);
        var topLeftOverlap = bitmap.GetPixel(35, 35);
        var topRightSingle = bitmap.GetPixel(95, 15);
        var topRightOverlap = bitmap.GetPixel(115, 35);
        var bottomLeftSingle = bitmap.GetPixel(15, 95);
        var bottomLeftOverlap = bitmap.GetPixel(35, 115);
        var bottomRightSingle = bitmap.GetPixel(95, 95);
        var bottomRightOverlap = bitmap.GetPixel(115, 115);

        Assert.True(topLeftSingle.Alpha > 100);
        Assert.True(topRightSingle.Alpha > 100);
        Assert.True(bottomLeftSingle.Alpha > 100);
        Assert.True(bottomRightSingle.Alpha > 100);

        Assert.True(topLeftOverlap.Alpha > topLeftSingle.Alpha + 40);
        Assert.True(topRightOverlap.Alpha > topRightSingle.Alpha + 40);
        Assert.True(bottomLeftOverlap.Alpha > bottomLeftSingle.Alpha + 40);
        Assert.InRange(System.Math.Abs(bottomRightOverlap.Alpha - bottomRightSingle.Alpha), 0, 2);
    }

    private const string OpacitySvg = """
        <?xml version="1.0" encoding="UTF-8" standalone="no"?>
        <svg height="200" xmlns="http://www.w3.org/2000/svg" version="1.1">
          <g transform="translate(0, 0)">
            <rect x="10" y="10" width="40" height="40" fill-opacity="0.5"/>
            <rect x="30" y="30" width="40" height="40" fill-opacity="0.5"/>
          </g>
          <g transform="translate(80, 0)" fill-opacity="0.5">
            <rect x="10" y="10" width="40" height="40"/>
            <rect x="30" y="30" width="40" height="40"/>
          </g>
          <g transform="translate(0, 80)">
            <rect x="10" y="10" width="40" height="40" opacity="0.5"/>
            <rect x="30" y="30" width="40" height="40" opacity="0.5"/>
          </g>
          <g transform="translate(80, 80)" opacity="0.5">
            <rect x="10" y="10" width="40" height="40"/>
            <rect x="30" y="30" width="40" height="40"/>
          </g>
          <text transform="translate(170,45)">fill-opacity</text>
          <text transform="translate(170,125)">opacity</text>
          <text transform="translate(10,175)">applied to</text>
          <text transform="translate(0,190)">each element</text>
          <text transform="translate(90,175)">applied to</text>
          <text transform="translate(103,190)">group</text>
        </svg>
        """;
}
