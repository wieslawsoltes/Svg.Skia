using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class SvgContextPaintServerTests
{
    [Fact]
    public void FromSvg_ParsesContextFillAndContextStrokePaintServers()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <marker id="marker" markerWidth="10" markerHeight="10">
                  <path id="marker-shape" d="M0,0 L10,5 L0,10 Z" fill="context-stroke" stroke="context-fill" />
                  <path id="style-shape" d="M0,0 L10,5 L0,10 Z" style="fill:context-fill;stroke:context-stroke" />
                </marker>
              </defs>
            </svg>
            """);

        var markerShape = Assert.IsType<SvgPath>(document!.GetElementById("marker-shape"));
        var fill = Assert.IsType<SvgContextPaintServer>(markerShape.Fill);
        var stroke = Assert.IsType<SvgContextPaintServer>(markerShape.Stroke);

        Assert.Equal(SvgContextPaintKind.Stroke, fill.Kind);
        Assert.Equal(SvgContextPaintKind.Fill, stroke.Kind);
        Assert.Equal("context-stroke", fill.ToString());
        Assert.Equal("context-fill", stroke.ToString());

        var styleShape = Assert.IsType<SvgPath>(document.GetElementById("style-shape"));
        Assert.Equal(SvgContextPaintKind.Fill, Assert.IsType<SvgContextPaintServer>(styleShape.Fill).Kind);
        Assert.Equal(SvgContextPaintKind.Stroke, Assert.IsType<SvgContextPaintServer>(styleShape.Stroke).Kind);
    }

    [Fact]
    public void SvgContextPaintServer_DeepCopyPreservesContextKind()
    {
        var server = new SvgContextPaintServer(SvgContextPaintKind.Stroke);

        var copy = Assert.IsType<SvgContextPaintServer>(server.DeepCopy());

        Assert.Equal(SvgContextPaintKind.Stroke, copy.Kind);
        Assert.Equal(server, copy);
    }
}
