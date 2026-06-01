using System;
using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgPaintServerRenderingTests
{
    [Fact]
    public void RetainedScene_StopCurrentColorDefaultsToBlack()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <defs>
                <linearGradient id="paint" x1="0" y1="0" x2="10" y2="0" gradientUnits="userSpaceOnUse">
                  <stop offset="0" stop-color="yellow" />
                  <stop offset="1" stop-color="currentColor" />
                </linearGradient>
              </defs>
              <rect id="target" x="0" y="0" width="50" height="40" fill="url(#paint)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var shader = GetFillShader<LinearGradientShader>(svg, "target");

        Assert.NotNull(shader.Colors);
        Assert.Equal(2, shader.Colors!.Length);
        Assert.Equal(0f, shader.Colors[1].Red);
        Assert.Equal(0f, shader.Colors[1].Green);
        Assert.Equal(0f, shader.Colors[1].Blue);
        Assert.Equal(1f, shader.Colors[1].Alpha);
    }

    [Fact]
    public void RetainedScene_RadialGradientCorrectsFocalPointOutsideCircle()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <radialGradient id="paint" gradientUnits="userSpaceOnUse" cx="10" cy="10" r="75" fx="83.33" fy="75">
                  <stop offset="0" stop-color="white" />
                  <stop offset="1" stop-color="black" />
                </radialGradient>
              </defs>
              <rect id="target" width="100" height="100" fill="url(#paint)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var shader = GetFillShader<TwoPointConicalGradientShader>(svg, "target");
        var dx = shader.Start.X - shader.End.X;
        var dy = shader.Start.Y - shader.End.Y;
        var correctedDistance = MathF.Sqrt((dx * dx) + (dy * dy));

        Assert.InRange(correctedDistance, 74.99f, 75.01f);
        Assert.True(shader.Start.X < 83.33f);
        Assert.True(shader.Start.Y < 75f);
    }

    private static TShader GetFillShader<TShader>(SKSvg svg, string sourceElementId)
        where TShader : SKShader
    {
        var command = svg.Model!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>(sourceElementId)
            .First(static candidate => candidate.Paint?.Style == SKPaintStyle.Fill);

        return Assert.IsType<TShader>(command.Paint!.Shader);
    }
}
