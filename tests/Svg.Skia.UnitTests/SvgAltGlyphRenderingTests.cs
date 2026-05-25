using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgAltGlyphRenderingTests
{
    [Fact]
    public void RetainedSceneGraph_AltGlyphDefRendersReferencedSvgFontGlyphPath()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <font id="AltFont" horiz-adv-x="10">
                  <font-face font-family="AltFont" units-per-em="10" ascent="10" descent="0" alphabetic="0" />
                  <glyph id="normal-a" unicode="A" horiz-adv-x="10" d="M0 0H10V10H0Z" />
                  <glyph id="wide-a" glyph-name="wideA" horiz-adv-x="20" d="M0 0H20V10H0Z" />
                </font>
                <altGlyphDef id="wideDef">
                  <glyphRef xlink:href="#wide-a" />
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="40" font-family="AltFont" font-size="10">
                <altGlyph id="ag" xlink:href="#wideDef">A</altGlyph>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var altGlyphPaths = GetVisiblePathCommands(retainedModel!, "ag");
        var bounds = UnionBounds(altGlyphPaths);

        Assert.NotEmpty(altGlyphPaths);
        Assert.Empty(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("ag"));
        Assert.InRange(bounds.Width, 19.9f, 20.1f);
    }

    [Fact]
    public void RetainedSceneGraph_AltGlyphGlyphRefAttributeRendersNamedSvgFontGlyphPath()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <font id="AltFont" horiz-adv-x="10">
                  <font-face font-family="AltFont" units-per-em="10" ascent="10" descent="0" alphabetic="0" />
                  <glyph id="normal-a" unicode="A" horiz-adv-x="10" d="M0 0H10V10H0Z" />
                  <glyph id="wide-a" glyph-name="wideA" horiz-adv-x="20" d="M0 0H20V10H0Z" />
                </font>
              </defs>
              <text id="label" x="10" y="40" font-family="AltFont" font-size="10">
                <altGlyph id="ag" glyphRef="wideA">A</altGlyph>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var altGlyphPaths = GetVisiblePathCommands(retainedModel!, "ag");
        var bounds = UnionBounds(altGlyphPaths);

        Assert.NotEmpty(altGlyphPaths);
        Assert.Empty(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("ag"));
        Assert.InRange(bounds.Width, 19.9f, 20.1f);
    }

    [Fact]
    public void RetainedSceneGraph_EmptyAltGlyphRendersReferencedSvgFontGlyphPath()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <font id="AltFont" horiz-adv-x="10">
                  <font-face font-family="AltFont" units-per-em="10" ascent="10" descent="0" alphabetic="0" />
                  <glyph id="normal-a" unicode="A" horiz-adv-x="10" d="M0 0H10V10H0Z" />
                  <glyph id="wide-a" glyph-name="wideA" unicode="W" horiz-adv-x="20" d="M0 0H20V10H0Z" />
                </font>
                <altGlyphDef id="wideDef">
                  <glyphRef xlink:href="#wide-a" />
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="40" font-family="AltFont" font-size="10">
                <altGlyph id="ag" xlink:href="#wideDef" />A
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var altGlyphPaths = GetVisiblePathCommands(retainedModel!, "ag");
        var bounds = UnionBounds(altGlyphPaths);

        Assert.NotEmpty(altGlyphPaths);
        Assert.Empty(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("ag"));
        Assert.InRange(bounds.Width, 19.9f, 20.1f);
    }

    [Fact]
    public void RetainedSceneGraph_AltGlyphItemSkipsInvalidCandidateAndRendersGlyphNameSequence()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <font id="AltFont" horiz-adv-x="8">
                  <font-face font-family="AltFont" units-per-em="10" ascent="10" descent="0" alphabetic="0" />
                  <glyph unicode="B" horiz-adv-x="8" d="M0 0H8V10H0Z" />
                  <glyph glyph-name="leftHalf" horiz-adv-x="10" d="M0 0H10V10H0Z" />
                  <glyph glyph-name="rightHalf" horiz-adv-x="12" d="M0 0H12V10H0Z" />
                </font>
                <altGlyphDef id="pairDef">
                  <altGlyphItem>
                    <glyphRef xlink:href="#missing-glyph" />
                  </altGlyphItem>
                  <altGlyphItem>
                    <glyphRef glyphRef="leftHalf" />
                    <glyphRef glyphRef="rightHalf" />
                  </altGlyphItem>
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="40" font-family="AltFont" font-size="10">
                <altGlyph id="ag" xlink:href="#pairDef">B</altGlyph>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var altGlyphPaths = GetVisiblePathCommands(retainedModel!, "ag");
        var bounds = UnionBounds(altGlyphPaths);

        Assert.NotEmpty(altGlyphPaths);
        Assert.Empty(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("ag"));
        Assert.InRange(bounds.Width, 21.9f, 22.1f);
    }

    [Fact]
    public void RetainedSceneGraph_InvalidAltGlyphReferenceFallsBackToOriginalSvgFontGlyph()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <font id="AltFont" horiz-adv-x="10">
                  <font-face font-family="AltFont" units-per-em="10" ascent="10" descent="0" alphabetic="0" />
                  <glyph id="normal-a" unicode="A" horiz-adv-x="10" d="M0 0H10V10H0Z" />
                  <glyph id="wide-a" glyph-name="wideA" horiz-adv-x="20" d="M0 0H20V10H0Z" />
                </font>
                <altGlyphDef id="badDef">
                  <glyphRef xlink:href="#missing-glyph" />
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="40" font-family="AltFont" font-size="10">
                <altGlyph id="ag" xlink:href="#badDef">A</altGlyph>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var fallbackPaths = GetVisiblePathCommands(retainedModel!, "ag");
        var bounds = UnionBounds(fallbackPaths);

        Assert.NotEmpty(fallbackPaths);
        Assert.Empty(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("ag"));
        Assert.InRange(bounds.Width, 9.9f, 10.1f);
    }

    [Fact]
    public void RetainedSceneGraph_CyclicAltGlyphReferenceFallsBackToOriginalSvgFontGlyph()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <font id="AltFont" horiz-adv-x="10">
                  <font-face font-family="AltFont" units-per-em="10" ascent="10" descent="0" alphabetic="0" />
                  <glyph id="normal-a" unicode="A" horiz-adv-x="10" d="M0 0H10V10H0Z" />
                </font>
                <altGlyphDef id="loopDef">
                  <glyphRef xlink:href="#loopDef" />
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="40" font-family="AltFont" font-size="10">
                <altGlyph id="ag" xlink:href="#loopDef">A</altGlyph>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var fallbackPaths = GetVisiblePathCommands(retainedModel!, "ag");
        var bounds = UnionBounds(fallbackPaths);

        Assert.NotEmpty(fallbackPaths);
        Assert.Empty(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("ag"));
        Assert.InRange(bounds.Width, 9.9f, 10.1f);
    }

    private static List<DrawPathCanvasCommand> GetVisiblePathCommands(SKPicture picture, string sourceElementId)
    {
        return picture
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>(sourceElementId)
            .Where(static command => command.Path is { IsEmpty: false })
            .ToList();
    }

    private static SKRect UnionBounds(IEnumerable<DrawPathCanvasCommand> commands)
    {
        return commands
            .Select(static command => command.Path!.Bounds)
            .Aggregate(SKRect.Empty, static (accumulator, bounds) =>
                accumulator.IsEmpty ? bounds : SKRect.Union(accumulator, bounds));
    }
}
