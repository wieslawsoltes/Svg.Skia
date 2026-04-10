using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Svg;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace Avalonia.Svg.UnitTests;

public class SvgSourceTests
{
    private const string SampleSvg = "<svg width=\"10\" height=\"10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";
    private const string SvgFontGlyphSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
          <defs>
            <style type="text/css"><![CDATA[
              @font-face {
                font-family: 'DefaultFont';
                src: url('#DefaultFontFace') format('svg');
              }
            ]]></style>
            <font id="DefaultFontFace" horiz-adv-x="100">
              <font-face font-family="DefaultFont" units-per-em="100" ascent="100" descent="0" />
              <glyph unicode="A" horiz-adv-x="100" d="M10 0H30V100H10Z" />
            </font>
          </defs>
          <text x="10" y="110" fill="black" font-family="DefaultFont" font-size="100">A</text>
        </svg>
        """;

    [AvaloniaFact]
    public void RebuildFromModel_RefreshesPicture()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var original = source.Picture;

        Assert.NotNull(original);
        var command = source.Picture?.FindCommands<DrawPathCanvasCommand>().FirstOrDefault();
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

    [AvaloniaFact]
    public void LoadFromSvg_UsesSvgGlyphPaths()
    {
        var source = SvgSource.LoadFromSvg(SvgFontGlyphSvg);

        Assert.NotNull(source.Picture);
        Assert.NotEmpty(source.Picture!.FindCommands<DrawPathCanvasCommand>());
        Assert.Empty(source.Picture.FindCommands<DrawTextCanvasCommand>());
        Assert.Empty(source.Picture.FindCommands<DrawTextBlobCanvasCommand>());
    }
}
