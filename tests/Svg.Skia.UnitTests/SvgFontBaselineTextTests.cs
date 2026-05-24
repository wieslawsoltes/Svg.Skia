using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgFontBaselineTextTests
{
    [Fact]
    public void SvgFontUseScriptBaseline_UsesMixedScriptFontFaceCoordinates()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="180" viewBox="0 0 220 180">
              <defs>
                <font id="BaselineFont" horiz-adv-x="1000">
                  <font-face font-family="BaselineFont" units-per-em="1000" ascent="800" descent="200" alphabetic="0" ideographic="-200" hanging="650" mathematical="300" x-height="500" cap-height="700" />
                  <missing-glyph horiz-adv-x="1000" d="M0 0H700V700H0Z" />
                  <glyph unicode="A" horiz-adv-x="1000" d="M0 0H700V700H0Z" />
                  <glyph unicode="&#x6F22;" horiz-adv-x="1000" d="M0 0H700V700H0Z" />
                  <glyph unicode="&#x923;" horiz-adv-x="1000" d="M0 0H700V700H0Z" />
                </font>
              </defs>
              <text id="latin" x="20" y="100" font-family="BaselineFont" font-size="40" dominant-baseline="use-script">A</text>
              <text id="cjk" x="80" y="100" font-family="BaselineFont" font-size="40" dominant-baseline="use-script">&#x6F22;</text>
              <text id="hanging" x="140" y="100" font-family="BaselineFont" font-size="40" dominant-baseline="use-script">&#x923;</text>
            </svg>
            """;

        var latin = GetOnlyPathBounds(svgMarkup, "latin");
        var cjk = GetOnlyPathBounds(svgMarkup, "cjk");
        var hanging = GetOnlyPathBounds(svgMarkup, "hanging");

        Assert.True(
            cjk.Bottom < latin.Bottom - 4f,
            $"Expected CJK baseline to sit above alphabetic fallback. latin={latin}, cjk={cjk}");
        Assert.True(
            hanging.Top > latin.Top + 10f,
            $"Expected hanging baseline to sit below alphabetic fallback. latin={latin}, hanging={hanging}");
    }

    [Fact]
    public void SvgFontBaselineFallback_UsesBrowserLikeFontCoordinatesWhenTableEntriesAreMissing()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="180" viewBox="0 0 260 180">
              <defs>
                <font id="FallbackFont" horiz-adv-x="1000">
                  <font-face font-family="FallbackFont" units-per-em="1000" ascent="800" descent="200" alphabetic="0" />
                  <missing-glyph horiz-adv-x="1000" d="M0 0H700V700H0Z" />
                  <glyph unicode="A" horiz-adv-x="1000" d="M0 0H700V700H0Z" />
                </font>
              </defs>
              <text id="alphabetic" x="20" y="100" font-family="FallbackFont" font-size="40" dominant-baseline="alphabetic">A</text>
              <text id="ideographic" x="80" y="100" font-family="FallbackFont" font-size="40" dominant-baseline="ideographic">A</text>
              <text id="central" x="140" y="100" font-family="FallbackFont" font-size="40" dominant-baseline="central">A</text>
              <text id="hanging" x="200" y="100" font-family="FallbackFont" font-size="40" dominant-baseline="hanging">A</text>
            </svg>
            """;

        var alphabetic = GetOnlyPathBounds(svgMarkup, "alphabetic");
        var ideographic = GetOnlyPathBounds(svgMarkup, "ideographic");
        var central = GetOnlyPathBounds(svgMarkup, "central");
        var hanging = GetOnlyPathBounds(svgMarkup, "hanging");

        Assert.True(
            ideographic.Bottom < alphabetic.Bottom - 4f,
            $"Expected missing ideographic table entry to fall below the alphabetic baseline in SVG font coordinates. alphabetic={alphabetic}, ideographic={ideographic}");
        Assert.True(
            central.Bottom > alphabetic.Bottom + 6f,
            $"Expected central fallback to use the ascent/descent center. alphabetic={alphabetic}, central={central}");
        Assert.True(
            hanging.Bottom > central.Bottom + 6f,
            $"Expected hanging fallback to remain above the central baseline table position. central={central}, hanging={hanging}");
    }

    [Fact]
    public void SvgFontVerticalWritingMode_DefaultsToCentralDominantBaseline()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="180" viewBox="0 0 180 180">
              <defs>
                <font id="VerticalFont" horiz-adv-x="1000">
                  <font-face font-family="VerticalFont" units-per-em="1000" ascent="800" descent="200" alphabetic="0" />
                  <missing-glyph horiz-adv-x="1000" d="M0 0H700V700H0Z" />
                  <glyph unicode="A" horiz-adv-x="1000" d="M0 0H700V700H0Z" />
                </font>
              </defs>
              <text id="alphabetic" x="40" y="100" font-family="VerticalFont" font-size="40" dominant-baseline="alphabetic">A</text>
              <text id="vertical" x="110" y="100" font-family="VerticalFont" font-size="40" writing-mode="vertical-rl">A</text>
            </svg>
            """;

        var alphabetic = GetOnlyPathBounds(svgMarkup, "alphabetic");
        var vertical = GetOnlyPathBounds(svgMarkup, "vertical");

        Assert.True(
            vertical.Bottom > alphabetic.Bottom + 6f,
            $"Expected vertical SVG-font text to default to a central dominant baseline. alphabetic={alphabetic}, vertical={vertical}");
    }

    private static SKRect GetOnlyPathBounds(string svgMarkup, string sourceElementId)
    {
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = true;
        svg.FromSvg(svgMarkup);

        Assert.NotNull(svg.Model);
        var commands = svg.Model!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>(sourceElementId)
            .Where(static command => command.Path is { IsEmpty: false })
            .ToList();
        var command = Assert.Single(commands);
        Assert.NotNull(command.Path);
        return command.Path!.Bounds;
    }
}
