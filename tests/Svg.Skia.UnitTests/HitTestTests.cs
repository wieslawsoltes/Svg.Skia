using System.IO;
using ShimSkiaSharp;
using System.Linq;
using Svg.Skia.UnitTests.Common;
using Svg.Skia;
using Svg.Model.Services;
using Xunit;

namespace Svg.Skia.UnitTests;

public class HitTestTests : SvgUnitTest
{
    private static string GetSvgPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    [Fact]
    public void HitTest_Point_Inner()
    {
        var svg = new SKSvg();
        using var _ = svg.Load(GetSvgPath("HitTest.svg"));

        var results = svg.HitTestElements(new SKPoint(40, 40));
        Assert.Contains("inner", results.Select(e => e.ID));
    }

    [Fact]
    public void HitTest_Point_OuterOnly()
    {
        var svg = new SKSvg();
        using var _ = svg.Load(GetSvgPath("HitTest.svg"));

        var results = svg.HitTestElements(new SKPoint(10, 10)).Select(e => e.ID).ToList();
        Assert.Equal(new[] { "outer", null }, results);
    }

    [Fact]
    public void IntersectsWith_Works()
    {
        var a = SKRect.Create(0,0,10,10);
        var b = SKRect.Create(5,5,5,5);
        Assert.True(HitTestService.IntersectsWith(a,b));
        var c = SKRect.Create(20,20,5,5);
        Assert.False(HitTestService.IntersectsWith(a,c));
    }

    [Fact]
    public void HitTest_Text_Point()
    {
        var svg = new SKSvg();
        using var _ = svg.Load(GetSvgPath("HitTestText.svg"));

        var results = svg.HitTestElements(new SKPoint(12, 20)).Select(e => e.ID).ToList();
        Assert.Contains("hello", results);
    }

    [Fact]
    public void HitTest_Line_Point()
    {
        var svg = new SKSvg();
        using var _ = svg.Load(GetSvgPath("HitTestLine.svg"));

        var results = svg.HitTestElements(new SKPoint(50, 12)).Select(e => e.ID).ToList();
        Assert.Contains("line", results);
    }

    [Fact]
    public void HitTest_PathQuad_Point()
    {
        var svg = new SKSvg();
        using var _ = svg.Load(GetSvgPath("HitTestQuad.svg"));

        var results = svg.HitTestElements(new SKPoint(50, 50)).Select(e => e.ID).ToList();
        Assert.Contains("quad", results);
    }

    [Fact]
    public void HitTest_PathCubic_Point()
    {
        var svg = new SKSvg();
        using var _ = svg.Load(GetSvgPath("HitTestCubic.svg"));

        var results = svg.HitTestElements(new SKPoint(50, 50)).Select(e => e.ID).ToList();
        Assert.Contains("cubic", results);
    }

    [Fact]
    public void HitTest_PathArc_Point()
    {
        var svg = new SKSvg();
        using var _ = svg.Load(GetSvgPath("HitTestArc.svg"));

        var results = svg.HitTestElements(new SKPoint(50, 70)).Select(e => e.ID).ToList();
        Assert.Contains("arc", results);
    }
}
