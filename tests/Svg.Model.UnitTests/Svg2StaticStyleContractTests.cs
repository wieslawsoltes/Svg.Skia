using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class Svg2StaticStyleContractTests
{
    [Fact]
    public void PresentationAttributes_ParseSvg2StaticStyleProperties()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <path id="shape"
                    d="M0,0 L10,0"
                    paint-order="stroke fill markers"
                    transform-box="fill-box"
                    transform-origin="50% 50%" />
              <text id="label"
                    white-space="pre-wrap"
                    text-overflow="ellipsis"
                    inline-size="32px"
                    shape-inside="url(#shape)"
                    shape-subtract="none">hello</text>
              <textPath id="pathText" path="M0 0 L10 0" side="right">hello</textPath>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);

        var shape = Assert.IsType<SvgPath>(document!.GetElementById("shape"));
        var label = Assert.IsType<SvgText>(document.GetElementById("label"));
        var pathText = Assert.IsType<SvgTextPath>(document.GetElementById("pathText"));

        shape.FlushStyles();
        label.FlushStyles();
        pathText.FlushStyles();

        Assert.Equal(SvgPaintOrder.StrokeFillMarkers, shape.PaintOrder);
        Assert.Equal(SvgTransformBox.FillBox, shape.TransformBox);
        Assert.Equal("50% 50%", shape.TransformOrigin);
        Assert.Equal(SvgWhiteSpace.PreWrap, label.WhiteSpace);
        Assert.Equal("ellipsis", label.TextOverflow);
        Assert.Equal("32px", label.InlineSize);
        Assert.Equal("url(#shape)", label.ShapeInside);
        Assert.Equal("none", label.ShapeSubtract);
        Assert.NotNull(pathText.PathData);
        Assert.Equal(2, pathText.PathData.Count);
        Assert.Equal(SvgTextPathSide.Right, pathText.Side);
    }

    [Fact]
    public void InlineStyle_PreservesSvg2StaticRendererPropertiesForLaterCompilation()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <g id="layer" style="isolation:isolate; mix-blend-mode:multiply; mask-type:alpha">
                <rect id="shape" width="10" height="10"
                      style="paint-order:markers stroke fill; white-space:break-spaces" />
              </g>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);

        var layer = Assert.IsType<SvgGroup>(document!.GetElementById("layer"));
        var shape = Assert.IsType<SvgRectangle>(document.GetElementById("shape"));

        layer.FlushStyles();
        shape.FlushStyles();

        Assert.True(layer.TryGetAttribute("isolation", out var isolation));
        Assert.Equal("isolate", isolation);
        Assert.True(layer.TryGetAttribute("mix-blend-mode", out var blendMode));
        Assert.Equal("multiply", blendMode);
        Assert.Equal(SvgIsolation.Isolate, layer.Isolation);
        Assert.Equal(SvgMixBlendMode.Multiply, layer.MixBlendMode);
        Assert.True(layer.TryGetAttribute("mask-type", out var maskType));
        Assert.Equal("alpha", maskType);
        Assert.Equal(SvgPaintOrder.MarkersStrokeFill, shape.PaintOrder);
        Assert.Equal(SvgWhiteSpace.BreakSpaces, shape.WhiteSpace);
    }

    [Fact]
    public void CompositingProperties_AreCssOnlyAndNotPresentationAttributes()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <g id="layer" isolation="isolate" mix-blend-mode="multiply" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);

        var layer = Assert.IsType<SvgGroup>(document!.GetElementById("layer"));
        Assert.False(layer.TryGetAttribute("isolation", out _));
        Assert.False(layer.TryGetAttribute("mix-blend-mode", out _));
    }

    [Fact]
    public void PaintOrder_InvalidChildDeclarationDoesNotOverrideInheritedValue()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <g id="parent" paint-order="stroke fill">
                <rect id="child" width="10" height="10" paint-order="stroke stroke" />
              </g>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var child = Assert.IsType<SvgRectangle>(document!.GetElementById("child"));

        child.FlushStyles();

        Assert.Equal(SvgPaintOrder.StrokeFillMarkers, child.PaintOrder);
    }

    [Fact]
    public void ComputedStyle_StylesheetOverridesPresentationAttributes()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <style>
                #shape { paint-order: markers stroke fill; }
                #label {
                  white-space: break-spaces;
                  text-overflow: ellipsis;
                  inline-size: 48px;
                  shape-inside: url(#shape);
                  shape-subtract: none;
                }
              </style>
              <path id="shape" d="M0,0 L10,0" paint-order="stroke fill markers" />
              <text id="label"
                    white-space="normal"
                    text-overflow="clip"
                    inline-size="12px"
                    shape-inside="none"
                    shape-subtract="url(#shape)">hello</text>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);

        var shape = Assert.IsType<SvgPath>(document!.GetElementById("shape"));
        var label = Assert.IsType<SvgText>(document.GetElementById("label"));

        shape.FlushStyles();
        label.FlushStyles();

        Assert.Equal(SvgPaintOrder.MarkersStrokeFill, shape.PaintOrder);
        Assert.Equal(SvgWhiteSpace.BreakSpaces, label.WhiteSpace);
        Assert.Equal("ellipsis", label.TextOverflow);
        Assert.Equal("48px", label.InlineSize);
        Assert.Equal("url(\"#shape\")", label.ShapeInside);
        Assert.Equal("none", label.ShapeSubtract);
    }

    [Fact]
    public void ComputedStyle_InvalidStylesheetDeclarationFallsBackToInheritedValue()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <style>#child { paint-order: stroke stroke; }</style>
              <g id="parent" paint-order="markers stroke fill">
                <rect id="child" width="10" height="10" />
              </g>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var child = Assert.IsType<SvgRectangle>(document!.GetElementById("child"));

        child.FlushStyles();

        Assert.Equal(SvgPaintOrder.MarkersStrokeFill, child.PaintOrder);
    }
}
