using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace Svg.Skia.UnitTests;

public class Svg2StaticSubsetRenderingContractTests
{
    [Theory]
    [InlineData("vector-effect=\"fixed-position\"")]
    [InlineData("style=\"vector-effect: non-rotation\"")]
    public void RetainedRenderer_UnsupportedVectorEffectValuesUseScalingStroke(string vectorEffect)
    {
        using var svg = new SKSvg();
        svg.FromSvg($$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40" viewBox="0 0 100 40">
              <path id="target" d="M10,20 L90,20" fill="none" stroke="black" stroke-width="4" {{vectorEffect}} />
            </svg>
            """);

        var strokeCommand = Assert.Single(
            svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"),
            static command => command.Paint?.Style == SKPaintStyle.Stroke);

        Assert.False(strokeCommand.Paint!.IsStrokeNonScaling);
    }

    [Fact]
    public void RetainedRenderer_StrokeLinejoinArcsFallsBackToMiterJoin()
    {
        using var svg = new SKSvg();
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80" viewBox="0 0 100 80">
              <path id="target" d="M10,70 L50,10 L90,70" fill="none" stroke="black" stroke-width="8" stroke-linejoin="arcs" />
            </svg>
            """);

        var strokeCommand = Assert.Single(
            svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"),
            static command => command.Paint?.Style == SKPaintStyle.Stroke);

        Assert.Equal(SKStrokeJoin.Miter, strokeCommand.Paint!.StrokeJoin);
    }

    [Fact]
    public void RetainedRenderer_UnknownSvgElementsDoNotCreateDrawCommands()
    {
        using var svg = new SKSvg();
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20" viewBox="0 0 40 20">
              <unknownStatic id="unknown" x="0" y="0" width="40" height="20" fill="red" />
              <rect id="known" x="4" y="4" width="8" height="8" fill="green" />
            </svg>
            """);

        var model = svg.Model!;

        Assert.Empty(model.FindCommandsBySourceElementId<DrawPathCanvasCommand>("unknown"));
        Assert.NotEmpty(model.FindCommandsBySourceElementId<DrawPathCanvasCommand>("known"));
    }
}
