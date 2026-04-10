using System.IO;
using System.Linq;
using Svg;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgMarkerParsingTests
{
    [Fact]
    public void PaintingMarker05_ShorthandMarkerStyleIsParsed()
    {
        var path = Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", "painting-marker-05-f.svg");
        var document = SvgDocument.Open<SvgDocument>(path);
        var markerPath = document.GetElementById<SvgPath>("p1");

        Assert.NotNull(markerPath);
        Assert.Equal("url(\"#marker1\")", markerPath!.Marker?.ToString());
        Assert.Equal("url(\"#marker1\")", markerPath.MarkerStart?.ToString());
        Assert.Equal("url(\"#marker1\")", markerPath.MarkerMid?.ToString());
        Assert.Equal("url(\"#marker1\")", markerPath.MarkerEnd?.ToString());
    }

    [Fact]
    public void PaintingMarker04_PresentationMarkerAttributeIsIgnored()
    {
        var path = Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", "painting-marker-04-f.svg");
        var document = SvgDocument.Open<SvgDocument>(path);
        var group = document.GetElementById<SvgGroup>("markme");

        Assert.NotNull(group);
        Assert.Null(group!.Marker);
        Assert.False(group.ContainsAttribute("marker"));
        Assert.False(group.ContainsAttribute("marker-start"));
        Assert.False(group.ContainsAttribute("marker-mid"));
        Assert.False(group.ContainsAttribute("marker-end"));
    }

    [Fact]
    public void PaintingMarkerProperties01_StylesheetMarkersAreResolved()
    {
        var path = Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", "painting-marker-properties-01-f.svg");
        var document = SvgDocument.Open<SvgDocument>(path);
        var testBody = document.Children.OfType<SvgGroup>().FirstOrDefault(static group => group.ID == "test-body-content");

        Assert.NotNull(testBody);

        var lines = testBody!.Children.OfType<SvgLine>().ToList();
        var paths = testBody.Children.OfType<SvgPath>().ToList();

        Assert.Equal(2, lines.Count);
        Assert.Equal(3, paths.Count);
        var startLine = lines[0];
        var endPath = paths[1];
        var midPath = paths[2];

        Assert.Equal("url(\"#markerTest\")", startLine.MarkerStart?.ToString());
        Assert.Equal("url(\"#markerTest\")", endPath.MarkerEnd?.ToString());
        Assert.Equal("url(\"#markerTest\")", midPath.MarkerMid?.ToString());
    }
}
