using ShimSkiaSharp;
using Svg;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class SvgPatternPaintStateResolverTests
{
    [Fact]
    public void TryCreate_ResolvesInheritedPatternGeometryAndViewBox()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <defs>
                <pattern id="base" width="10" height="20" patternUnits="userSpaceOnUse" viewBox="0 0 5 10">
                  <rect id="pattern-rect" x="0" y="0" width="5" height="10" fill="red" />
                </pattern>
                <pattern id="derived" x="3" y="4" href="#base" />
              </defs>
              <rect id="target" x="0" y="0" width="50" height="40" fill="url(#derived)" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var target = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var derived = Assert.IsType<SvgPatternServer>(document.GetElementById("derived"));

        var resolved = SvgPatternPaintStateResolver.TryCreate(
            derived,
            target,
            SKRect.Create(0f, 0f, 50f, 40f),
            out var state);

        Assert.True(resolved);
        Assert.NotNull(state);
        Assert.Equal("base", state!.ContentSource.ID);
        Assert.Single(state.Children);
        Assert.Equal("pattern-rect", state.Children[0].ID);

        Assert.Equal(3f, state.PatternRect.Left);
        Assert.Equal(4f, state.PatternRect.Top);
        Assert.Equal(10f, state.PatternRect.Width);
        Assert.Equal(20f, state.PatternRect.Height);

        Assert.Equal(10f, state.PictureViewport.Width);
        Assert.Equal(20f, state.PictureViewport.Height);
        Assert.Equal(3f, state.ShaderMatrix.TransX);
        Assert.Equal(4f, state.ShaderMatrix.TransY);
        Assert.Equal(2f, state.PictureTransform.ScaleX);
        Assert.Equal(2f, state.PictureTransform.ScaleY);
    }
}
