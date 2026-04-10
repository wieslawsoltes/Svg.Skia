using System.IO;
using System.Linq;
using ShimSkiaSharp;
using Svg;
using Svg.Model.Services;
using Svg.Skia;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class HitTestTests : SvgUnitTest
{
    private const string StrokeOnlyHitTestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <rect id="back" x="0" y="0" width="100" height="100" fill="green" />
          <rect id="front" x="10" y="10" width="80" height="80" fill="none" stroke="red" stroke-width="10" />
        </svg>
        """;

    private const string ClipPathHitTestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <defs>
            <clipPath id="clip">
              <circle cx="50" cy="50" r="20" />
            </clipPath>
          </defs>
          <rect id="back" x="0" y="0" width="100" height="100" fill="green" />
          <rect id="front" x="0" y="0" width="100" height="100" fill="red" clip-path="url(#clip)" />
        </svg>
        """;

    private const string ReferencedClipPathHitTestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <defs>
            <clipPath id="clip-base">
              <circle cx="50" cy="50" r="20" />
            </clipPath>
            <clipPath id="clip-ref" clip-path="url(#clip-base)">
            </clipPath>
          </defs>
          <rect id="back" x="0" y="0" width="100" height="100" fill="green" />
          <rect id="front" x="0" y="0" width="100" height="100" fill="red" clip-path="url(#clip-ref)" />
        </svg>
        """;

    private const string MaskHitTestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
          <defs>
            <mask id="half-mask" maskUnits="userSpaceOnUse" x="0" y="0" width="40" height="40">
              <rect x="0" y="0" width="20" height="40" fill="white" />
            </mask>
          </defs>
          <rect id="target" x="0" y="0" width="40" height="40" fill="red" mask="url(#half-mask)" />
        </svg>
        """;

    private const string MaskPointerEventsHitTestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
          <defs>
            <mask id="half-mask" maskUnits="userSpaceOnUse" x="0" y="0" width="40" height="40">
              <rect x="0" y="0" width="20" height="40" fill="white" pointer-events="none" />
            </mask>
          </defs>
          <rect id="target" x="0" y="0" width="40" height="40" fill="red" mask="url(#half-mask)" />
        </svg>
        """;

    private const string UseHitTestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <defs>
            <rect id="template" x="0" y="0" width="10" height="10" fill="red" />
          </defs>
          <use id="instance" xlink:href="#template" />
        </svg>
        """;

    private const string RetainedPointerEventsSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">
          <rect id="target"
                x="2"
                y="2"
                width="20"
                height="20"
                fill="red"
                stroke="black"
                stroke-width="2"
                pointer-events="none" />
        </svg>
        """;

    private const string HiddenContainerHitTestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20">
          <rect id="back" x="0" y="0" width="40" height="20" fill="green" />
          <g id="hidden-group" display="none">
            <rect id="hidden-front" x="0" y="0" width="40" height="20" fill="red" />
          </g>
        </svg>
        """;

    private const string InvalidFilterHitTestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20">
          <defs>
            <filter id="invalid-filter">
              <feDiffuseLighting />
            </filter>
          </defs>
          <rect id="back" x="0" y="0" width="40" height="20" fill="green" />
          <rect id="filtered-front" x="0" y="0" width="40" height="20" fill="red" filter="url(#invalid-filter)" />
        </svg>
        """;

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
        Assert.Equal(new[] { "outer" }, results);
    }

    [Fact]
    public void IntersectsWith_Works()
    {
        var a = SKRect.Create(0, 0, 10, 10);
        var b = SKRect.Create(5, 5, 5, 5);
        Assert.True(IntersectsWith(a, b));
        var c = SKRect.Create(20, 20, 5, 5);
        Assert.False(IntersectsWith(a, c));
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

        var results = svg.HitTestElements(new SKPoint(50, 30)).Select(e => e.ID).ToList();
        Assert.Contains("cubic", results);
    }

    [Fact]
    public void HitTest_PathArc_Point()
    {
        var svg = new SKSvg();
        using var _ = svg.Load(GetSvgPath("HitTestArc.svg"));

        var results = svg.HitTestElements(new SKPoint(50, 10)).Select(e => e.ID).ToList();
        Assert.Contains("arc", results);
    }

    [Fact]
    public void HitTest_Point_StrokeOnlyShape_ExcludesInterior()
    {
        using var svg = new SKSvg();
        svg.FromSvg(StrokeOnlyHitTestSvg);

        var results = svg.HitTestElements(new SKPoint(50, 50)).Select(e => e.ID).ToList();

        Assert.DoesNotContain("front", results);
        Assert.Contains("back", results);
    }

    [Fact]
    public void HitTest_Point_ClipPath_ExcludesOutsideClip()
    {
        using var svg = new SKSvg();
        svg.FromSvg(ClipPathHitTestSvg);

        var results = svg.HitTestElements(new SKPoint(10, 10)).Select(e => e.ID).ToList();

        Assert.DoesNotContain("front", results);
        Assert.Contains("back", results);
    }

    [Fact]
    public void HitTest_Point_ReferencedClipPath_UsesReferencedGeometry()
    {
        using var svg = new SKSvg();
        svg.FromSvg(ReferencedClipPathHitTestSvg);

        var insideResults = svg.HitTestElements(new SKPoint(50, 50)).Select(e => e.ID).ToList();
        var outsideResults = svg.HitTestElements(new SKPoint(10, 10)).Select(e => e.ID).ToList();

        Assert.Contains("front", insideResults);
        Assert.DoesNotContain("front", outsideResults);
        Assert.Contains("back", outsideResults);
    }

    [Fact]
    public void HitTest_Point_MaskContainerWithoutPaint_DoesNotCountAsVisible()
    {
        using var svg = new SKSvg();
        svg.FromSvg(MaskHitTestSvg);

        var insideResults = svg.HitTestElements(new SKPoint(10, 20)).Select(e => e.ID).ToList();
        var outsideResults = svg.HitTestElements(new SKPoint(30, 20)).Select(e => e.ID).ToList();

        Assert.Contains("target", insideResults);
        Assert.DoesNotContain("target", outsideResults);
    }

    [Fact]
    public void HitTest_Point_MaskCoverage_IgnoresPointerEventSemantics()
    {
        using var svg = new SKSvg();
        svg.FromSvg(MaskPointerEventsHitTestSvg);

        var insideResults = svg.HitTestElements(new SKPoint(10, 20)).Select(e => e.ID).ToList();
        var outsideResults = svg.HitTestElements(new SKPoint(30, 20)).Select(e => e.ID).ToList();

        Assert.Contains("target", insideResults);
        Assert.DoesNotContain("target", outsideResults);
    }

    [Fact]
    public void HitTest_Point_UseElement_ReturnsOwningUseElement()
    {
        using var svg = new SKSvg();
        svg.FromSvg(UseHitTestSvg);

        var results = svg.HitTestElements(new SKPoint(2, 2)).Select(e => e.ID).ToList();

        Assert.Contains("instance", results);
        Assert.DoesNotContain("template", results);
    }

    [Fact]
    public void HitTest_Point_UsesRetainedPointerEventState_NotLiveDomState()
    {
        using var svg = new SKSvg();
        svg.FromSvg(RetainedPointerEventsSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        var sourceDocument = scene!.SourceDocument;
        Assert.NotNull(sourceDocument);

        var target = Assert.IsType<SvgRectangle>(sourceDocument.GetElementById("target"));
        var initialTarget = svg.HitTestTopmostElement(new SKPoint(12, 12));
        Assert.Null(initialTarget);

        target.PointerEvents = SvgPointerEvents.All;

        var retainedTarget = svg.HitTestTopmostElement(new SKPoint(12, 12));
        Assert.Null(retainedTarget);
    }

    [Fact]
    public void HitTest_Point_HiddenContainerDescendants_AreNotInteractable()
    {
        using var svg = new SKSvg();
        svg.FromSvg(HiddenContainerHitTestSvg);

        var hitElements = svg.HitTestElements(new SKPoint(10, 10)).Select(e => e.ID).ToList();
        var topmostElement = svg.HitTestTopmostElement(new SKPoint(10, 10));
        var topmostNode = svg.HitTestTopmostSceneNode(new SKPoint(10, 10));

        Assert.DoesNotContain("hidden-front", hitElements);
        Assert.NotNull(topmostElement);
        Assert.Equal("back", topmostElement!.ID);
        Assert.NotNull(topmostNode);
        Assert.Equal("back", topmostNode!.ElementId);
    }

    [Fact]
    public void HitTest_Point_InvalidFilterSubtree_IsNotInteractable()
    {
        using var svg = new SKSvg();
        svg.FromSvg(InvalidFilterHitTestSvg);

        var hitElements = svg.HitTestElements(new SKPoint(10, 10)).Select(e => e.ID).ToList();
        var topmostElement = svg.HitTestTopmostElement(new SKPoint(10, 10));
        var topmostNode = svg.HitTestTopmostSceneNode(new SKPoint(10, 10));

        Assert.DoesNotContain("filtered-front", hitElements);
        Assert.NotNull(topmostElement);
        Assert.Equal("back", topmostElement!.ID);
        Assert.NotNull(topmostNode);
        Assert.Equal("back", topmostNode!.ElementId);
    }

    private static bool IntersectsWith(SKRect a, SKRect b)
    {
        return a.Left < b.Right && a.Right > b.Left &&
               a.Top < b.Bottom && a.Bottom > b.Top;
    }
}
