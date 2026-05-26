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

}
