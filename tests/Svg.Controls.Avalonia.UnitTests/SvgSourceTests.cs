using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Svg;
using ShimSkiaSharp;
using Xunit;

namespace Avalonia.Svg.UnitTests;

public class SvgSourceTests
{
    private const string SampleSvg = "<svg width=\"10\" height=\"10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";

    [AvaloniaFact]
    public void RebuildFromModel_RefreshesPicture()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var original = source.Picture;

        Assert.NotNull(original);
        var command = source.Picture?.Commands?.OfType<DrawPathCanvasCommand>().FirstOrDefault();
        Assert.NotNull(command);

        if (command?.Paint is { } paint)
        {
            paint.Color = new SKColor(0, 0, 0, 255);
        }

        source.RebuildFromModel();

        Assert.NotNull(source.Picture);
        Assert.NotSame(original, source.Picture);
    }

    [AvaloniaFact]
    public void Clone_DeepClonesPicture()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var clone = source.Clone();

        Assert.NotSame(source, clone);
        Assert.NotSame(source.Picture, clone.Picture);
    }
}
