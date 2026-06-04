using System;
using ShimSkiaSharp;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class Svg2StaticPaintServerTests
{
    [Fact]
    public void PatternResolver_AppliesPatternTransformFromStylesheet()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <style>
                #base { patternTransform: translate(5 7); }
              </style>
              <defs>
                <pattern id="base" x="3" y="4" width="10" height="20" patternUnits="userSpaceOnUse">
                  <rect id="pattern-rect" x="0" y="0" width="10" height="20" fill="red" />
                </pattern>
              </defs>
              <rect id="target" x="0" y="0" width="50" height="40" fill="url(#base)" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var target = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var pattern = Assert.IsType<SvgPatternServer>(document.GetElementById("base"));

        var resolved = SvgPatternPaintStateResolver.TryCreate(
            pattern,
            target,
            SKRect.Create(0f, 0f, 50f, 40f),
            out var state);

        Assert.True(resolved);
        Assert.NotNull(state);
        Assert.Equal(8f, state!.ShaderMatrix.TransX);
        Assert.Equal(11f, state.ShaderMatrix.TransY);
    }

    [Fact]
    public void LinearGradient_AppliesGradientTransformFromStylesheet()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <style>
                #paint { gradientTransform: translate(6 8); }
              </style>
              <defs>
                <linearGradient id="paint" x1="0" y1="0" x2="10" y2="0" gradientUnits="userSpaceOnUse">
                  <stop offset="0" stop-color="red" />
                  <stop offset="1" stop-color="blue" />
                </linearGradient>
              </defs>
              <rect id="target" x="0" y="0" width="50" height="40" fill="url(#paint)" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var target = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var gradient = Assert.IsType<SvgLinearGradientServer>(document.GetElementById("paint"));

        var shader = Assert.IsType<LinearGradientShader>(
            PaintingService.CreateLinearGradient(
                gradient,
                SKRect.Create(0f, 0f, 50f, 40f),
                target,
                1f,
                DrawAttributes.None,
                SKColorSpace.Srgb));

        Assert.NotNull(shader.LocalMatrix);
        Assert.Equal(6f, shader.LocalMatrix!.Value.TransX);
        Assert.Equal(8f, shader.LocalMatrix.Value.TransY);
    }

    [Fact]
    public void LinearGradient_ExplicitDefaultSpreadMethodOverridesReferencedGradient()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <defs>
                <linearGradient id="base" spreadMethod="reflect">
                  <stop offset="0" stop-color="red" />
                  <stop offset="1" stop-color="blue" />
                </linearGradient>
                <linearGradient id="paint" href="#base" spreadMethod="pad" />
              </defs>
              <rect id="target" x="0" y="0" width="50" height="40" fill="url(#paint)" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var target = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var gradient = Assert.IsType<SvgLinearGradientServer>(document.GetElementById("paint"));

        var shader = Assert.IsType<LinearGradientShader>(
            PaintingService.CreateLinearGradient(
                gradient,
                SKRect.Create(0f, 0f, 50f, 40f),
                target,
                1f,
                DrawAttributes.None,
                SKColorSpace.Srgb));

        Assert.Equal(SKShaderTileMode.Clamp, shader.Mode);
    }

    [Fact]
    public void RadialGradient_InheritsStylesheetFocalRadiusThroughHrefChain()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <style>
                #base { fr: 6px; }
                #paint { gradientTransform: translate(2 3); }
              </style>
              <defs>
                <radialGradient id="base" cx="50" cy="50" r="40" gradientUnits="userSpaceOnUse">
                  <stop offset="0" stop-color="red" />
                  <stop offset="1" stop-color="blue" />
                </radialGradient>
                <radialGradient id="paint" href="#base" />
              </defs>
              <rect id="target" x="0" y="0" width="50" height="40" fill="url(#paint)" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var target = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var gradient = Assert.IsType<SvgRadialGradientServer>(document.GetElementById("paint"));

        var shader = Assert.IsType<TwoPointConicalGradientShader>(
            PaintingService.CreateTwoPointConicalGradient(
                gradient,
                SKRect.Create(0f, 0f, 50f, 40f),
                target,
                1f,
                DrawAttributes.None,
                SKColorSpace.Srgb));

        Assert.Equal(6f, shader.StartRadius);
        Assert.Equal(40f, shader.EndRadius);
        Assert.NotNull(shader.LocalMatrix);
        Assert.Equal(2f, shader.LocalMatrix!.Value.TransX);
        Assert.Equal(3f, shader.LocalMatrix.Value.TransY);
    }

    [Fact]
    public void StopOpacity_AcceptsPercentageValues()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <defs>
                <linearGradient id="paint">
                  <stop id="stop" offset="1" stop-color="black" stop-opacity="50%" />
                </linearGradient>
              </defs>
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var stop = Assert.IsType<SvgGradientStop>(document!.GetElementById("stop"));

        Assert.Equal(0.5f, stop.StopOpacity, precision: 3);
    }

    [Fact]
    public void StopColor_CurrentColorFallsBackToBlackWithoutDeclaredColor()
    {
        const string svg = """
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

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var target = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var gradient = Assert.IsType<SvgLinearGradientServer>(document.GetElementById("paint"));

        var shader = Assert.IsType<LinearGradientShader>(
            PaintingService.CreateLinearGradient(
                gradient,
                SKRect.Create(0f, 0f, 50f, 40f),
                target,
                1f,
                DrawAttributes.None,
                SKColorSpace.Srgb));

        Assert.NotNull(shader.Colors);
        Assert.Equal(2, shader.Colors!.Length);
        Assert.Equal(0f, shader.Colors[1].Red);
        Assert.Equal(0f, shader.Colors[1].Green);
        Assert.Equal(0f, shader.Colors[1].Blue);
        Assert.Equal(1f, shader.Colors[1].Alpha);
    }

    [Fact]
    public void StopColor_CurrentColorUsesGradientStylesheetColor()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <style>
                #paint { color: green; }
              </style>
              <defs>
                <linearGradient id="paint" x1="0" y1="0" x2="10" y2="0" gradientUnits="userSpaceOnUse">
                  <stop offset="0" stop-color="yellow" />
                  <stop offset="1" stop-color="currentColor" />
                </linearGradient>
              </defs>
              <rect id="target" x="0" y="0" width="50" height="40" fill="url(#paint)" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var target = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var gradient = Assert.IsType<SvgLinearGradientServer>(document.GetElementById("paint"));

        var shader = Assert.IsType<LinearGradientShader>(
            PaintingService.CreateLinearGradient(
                gradient,
                SKRect.Create(0f, 0f, 50f, 40f),
                target,
                1f,
                DrawAttributes.None,
                SKColorSpace.Srgb));

        Assert.NotNull(shader.Colors);
        Assert.Equal(2, shader.Colors!.Length);
        Assert.True(shader.Colors[1].Green > 0f);
        Assert.Equal(0f, shader.Colors[1].Red);
        Assert.Equal(0f, shader.Colors[1].Blue);
        Assert.Equal(1f, shader.Colors[1].Alpha);
    }

    [Fact]
    public void RadialGradient_CorrectsFocalPointOutsideCircle()
    {
        const string svg = """
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

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var target = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var gradient = Assert.IsType<SvgRadialGradientServer>(document.GetElementById("paint"));

        var shader = Assert.IsType<TwoPointConicalGradientShader>(
            PaintingService.CreateTwoPointConicalGradient(
                gradient,
                SKRect.Create(0f, 0f, 100f, 100f),
                target,
                1f,
                DrawAttributes.None,
                SKColorSpace.Srgb));

        var dx = shader.Start.X - shader.End.X;
        var dy = shader.Start.Y - shader.End.Y;
        var correctedDistance = MathF.Sqrt((dx * dx) + (dy * dy));

        Assert.InRange(correctedDistance, 74.99f, 75.01f);
        Assert.True(shader.Start.X < 83.33f);
        Assert.True(shader.Start.Y < 75f);
    }

}
