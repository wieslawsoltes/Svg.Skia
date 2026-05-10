using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using SkiaSharp;
using Xunit;
using ShimPaintStyle = ShimSkiaSharp.SKPaintStyle;
using ShimPoint = ShimSkiaSharp.SKPoint;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaColor = SkiaSharp.SKColor;
using SkiaColorSpace = SkiaSharp.SKColorSpace;
using SkiaColorType = SkiaSharp.SKColorType;
using SkiaPicture = SkiaSharp.SKPicture;

namespace Svg.Skia.UnitTests;

public class Issue441Tests
{
    [Fact]
    public void CssLinearGradientStroke_ResolvesStylesheetPaintServerAndRendersStroke()
    {
        using var svg = new SKSvg();
        svg.FromSvg(Issue441Svg);

        Assert.NotNull(svg.Model);
        Assert.NotNull(svg.Picture);

        var gradientStroke = svg.Model!
            .FindCommands<DrawPathCanvasCommand>()
            .Single(command => command.Paint?.Shader is LinearGradientShader);
        AssertIssue441GradientStroke(gradientStroke);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var retainedGradientStroke = retainedModel!
            .FindCommands<DrawPathCanvasCommand>()
            .Single(command => command.Paint?.Shader is LinearGradientShader);
        AssertIssue441GradientStroke(retainedGradientStroke);

        using var bitmap = RenderBitmap(svg.Picture!, 4f);
        AssertContainsYellowPixel(bitmap, 16, 16, 24, 28);
    }

    private static void AssertIssue441GradientStroke(DrawPathCanvasCommand gradientStroke)
    {
        var shader = Assert.IsType<LinearGradientShader>(gradientStroke.Paint!.Shader);
        Assert.Equal(ShimPaintStyle.Stroke, gradientStroke.Paint.Style);
        Assert.Equal(3f, gradientStroke.Paint.StrokeWidth);
        Assert.Equal(new ShimPoint(6.5f, 5f), shader.Start);
        Assert.Equal(new ShimPoint(6.5f, 17f), shader.End);
        Assert.NotNull(shader.Colors);
        Assert.NotNull(shader.ColorPos);
        Assert.Equal(6, shader.Colors!.Length);
        Assert.Equal(shader.Colors.Length, shader.ColorPos!.Length);
        AssertGradientStops(shader.ColorPos);
        AssertYellow(shader.Colors[0]);
    }

    private static void AssertGradientStops(float[] colorPos)
    {
        float[] expected = [0.16f, 0.33f, 0.72f, 0.86f, 0.9f, 1f];

        Assert.Equal(expected.Length, colorPos.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], colorPos[i], precision: 3);
        }
    }

    private static void AssertYellow(ShimSkiaSharp.SKColorF color)
    {
        Assert.True(color.Red > 0.9f, $"Expected yellow red channel but got {color}.");
        Assert.True(color.Green > 0.9f, $"Expected yellow green channel but got {color}.");
        Assert.True(color.Blue < 0.2f, $"Expected yellow blue channel but got {color}.");
    }

    private static void AssertContainsYellowPixel(SkiaBitmap bitmap, int left, int top, int width, int height)
    {
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (IsYellowPixel(color))
                {
                    return;
                }
            }
        }

        Assert.Fail("Expected the rendered CSS gradient stroke to contain a visible yellow pixel.");
    }

    private static bool IsYellowPixel(SkiaColor color)
    {
        return color.Alpha > 180
            && color.Red > 180
            && color.Green > 170
            && color.Blue < 100;
    }

    private static SkiaBitmap RenderBitmap(SkiaPicture picture, float scale)
    {
        var bitmap = picture.ToBitmap(
            SKColors.Transparent,
            scale,
            scale,
            SkiaColorType.Rgba8888,
            SkiaAlphaType.Unpremul,
            SkiaColorSpace.CreateSrgb());

        return Assert.IsType<SkiaBitmap>(bitmap);
    }

    private const string Issue441Svg = """
        <?xml version="1.0" encoding="UTF-8"?>
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 24">
          <defs>
            <style>
              .cls-3{fill:none;stroke:url(#linear-gradient);stroke-width:3px;stroke-miterlimit:10;}
            </style>
            <linearGradient id="linear-gradient" x1="6.5" y1="5" x2="6.5" y2="17" gradientUnits="userSpaceOnUse">
              <stop offset=".16" stop-color="#ffff29"/>
              <stop offset=".33" stop-color="#cfce26"/>
              <stop offset=".72" stop-color="#585421"/>
              <stop offset=".86" stop-color="#2a2620"/>
              <stop offset=".9" stop-color="#1e1b17"/>
              <stop offset="1" stop-color="#000"/>
            </linearGradient>
          </defs>
          <line class="cls-3" x1="6.5" y1="5" x2="6.5" y2="17"/>
        </svg>
        """;
}
