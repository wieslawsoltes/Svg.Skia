using System.Linq;
using ShimSkiaSharp;
using Svg.Skia;
using Xunit;

namespace Svg.Skia.UnitTests;

public class VectorEffectTests
{
    [Fact]
    public void NonScalingStroke_AdjustsWidth()
    {
        string svgText = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"20\"><g transform=\"scale(2)\"><line x1=\"0\" y1=\"10\" x2=\"40\" y2=\"10\" stroke=\"black\" stroke-width=\"10\" vector-effect=\"non-scaling-stroke\" /></g></svg>";
        var svg = new SKSvg();
        svg.FromSvg(svgText);
        Assert.NotNull(svg.Model);
        var draw = svg.Model!.Commands!.OfType<DrawPathCanvasCommand>().First();
        Assert.Equal(5f, draw.Paint!.StrokeWidth);
    }

    [Fact]
    public void ScalingStroke_DefaultBehavior()
    {
        string svgText = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"20\"><g transform=\"scale(2)\"><line x1=\"0\" y1=\"10\" x2=\"40\" y2=\"10\" stroke=\"black\" stroke-width=\"10\" /></g></svg>";
        var svg = new SKSvg();
        svg.FromSvg(svgText);
        Assert.NotNull(svg.Model);
        var draw = svg.Model!.Commands!.OfType<DrawPathCanvasCommand>().First();
        Assert.Equal(10f, draw.Paint!.StrokeWidth);
    }
}
