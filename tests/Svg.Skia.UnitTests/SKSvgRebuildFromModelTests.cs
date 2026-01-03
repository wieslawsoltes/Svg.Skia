using System.Linq;
using ShimSkiaSharp;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgRebuildFromModelTests
{
    private const string SampleSvg = "<svg width=\"10\" height=\"10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";

    [Fact]
    public void RebuildFromModel_RecreatesPicture()
    {
        var svg = new SKSvg();
        svg.FromSvg(SampleSvg);

        var original = svg.Picture;
        Assert.NotNull(original);

        var command = svg.Model?.Commands?.OfType<DrawPathCanvasCommand>().FirstOrDefault();
        Assert.NotNull(command);

        if (command?.Paint is { } paint)
        {
            paint.Color = new SKColor(0, 0, 0, 255);
        }

        var rebuilt = svg.RebuildFromModel();

        Assert.NotNull(rebuilt);
        Assert.NotSame(original, rebuilt);
        Assert.Same(rebuilt, svg.Picture);
    }

    [Fact]
    public void RebuildFromModel_ReturnsNull_WhenModelMissing()
    {
        var svg = new SKSvg();

        Assert.Null(svg.RebuildFromModel());
    }
}
