using ShimSkiaSharp;
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
    public void Svg11_PresentationMixBlendModeAttribute_RemainsIgnoredOnVisualElement()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" mix-blend-mode="multiply" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);

        var rect = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        Assert.False(rect.TryGetAttribute("mix-blend-mode", out _));
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

    [Fact]
    public void ComputedStyle_InvalidInlineSvg2DeclarationsDoNotOverrideFallbacks()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <clipPath id="clip"><rect width="10" height="10" /></clipPath>
                <filter id="shadow"><feDropShadow dx="1" dy="1" /></filter>
                <mask id="fade"><rect width="10" height="10" fill="white" /></mask>
                <marker id="start" markerWidth="4" markerHeight="4"><path d="M0,0 L4,2 L0,4 Z" /></marker>
                <marker id="end" markerWidth="4" markerHeight="4"><path d="M0,0 L4,2 L0,4 Z" /></marker>
              </defs>
              <text id="label"
                    white-space="pre-wrap"
                    text-overflow="ellipsis"
                    inline-size="32px"
                    style="white-space: sideways; text-overflow: overflow; inline-size: definitely-not-a-length">hello</text>
              <rect id="box" x="5" y="6" width="10" height="12"
                    style="x: not-a-length; y: 18px; width: bogus; height: 20px" />
              <path id="shape"
                    d="M0,0 L10,0"
                    clip-path="url(#clip)"
                    marker-start="url(#start)"
                    style="clip-path: url(); filter: url(#shadow); mask: url(#fade); marker-start: url(); marker-end: url(#end)" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);

        var label = Assert.IsType<SvgText>(document!.GetElementById("label"));
        var box = Assert.IsType<SvgRectangle>(document.GetElementById("box"));
        var shape = Assert.IsType<SvgPath>(document.GetElementById("shape"));

        label.FlushStyles();
        box.FlushStyles();
        shape.FlushStyles();

        Assert.Equal(SvgWhiteSpace.PreWrap, label.WhiteSpace);
        Assert.Equal("ellipsis", label.TextOverflow);
        Assert.Equal("32px", label.InlineSize);
        Assert.Equal(5f, box.X.Value);
        Assert.Equal(18f, box.Y.Value);
        Assert.Equal(10f, box.Width.Value);
        Assert.Equal(20f, box.Height.Value);
        Assert.Equal("url(#clip)", shape.ClipPath?.ToString());
        Assert.Equal("url(#shadow)", shape.Filter?.ToString());
        Assert.True(shape.TryGetAttribute("mask", out var mask));
        Assert.Equal("url(#fade)", mask);
        Assert.Equal("url(#start)", shape.MarkerStart?.ToString());
        Assert.Equal("url(#end)", shape.MarkerEnd?.ToString());
    }

    [Fact]
    public void ComputedStyle_StylesheetReferenceAndPathFallbacksAreValidatedBeforeFlush()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <style>
                #shape {
                  d: path("this is not path data");
                  clip-path: url();
                  marker-start: url();
                  marker-mid: url(#mid);
                  color-interpolation-filters: sideways;
                }
              </style>
              <defs>
                <clipPath id="clip"><rect width="10" height="10" /></clipPath>
                <marker id="start" markerWidth="4" markerHeight="4"><path d="M0,0 L4,2 L0,4 Z" /></marker>
                <marker id="mid" markerWidth="4" markerHeight="4"><path d="M0,0 L4,2 L0,4 Z" /></marker>
              </defs>
              <path id="shape"
                    d="M0,0 L10,0"
                    clip-path="url(#clip)"
                    marker-start="url(#start)"
                    color-interpolation-filters="sRGB" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var shape = Assert.IsType<SvgPath>(document!.GetElementById("shape"));

        shape.FlushStyles();

        Assert.Equal(2, shape.PathData.Count);
        Assert.Equal("url(#clip)", shape.ClipPath?.ToString());
        Assert.Equal("url(#start)", shape.MarkerStart?.ToString());
        Assert.Equal("url(#mid)", shape.MarkerMid?.ToString());
        Assert.Equal(Svg.DataTypes.SvgColourInterpolation.SRGB, shape.ColorInterpolationFilters);
    }

    [Fact]
    public void ComputedStyle_PathDataNoneSuppressesAttributeGeometry()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <style>#shape { d: none; }</style>
              <path id="shape" d="M0,0 L10,0" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var shape = Assert.IsType<SvgPath>(document!.GetElementById("shape"));

        Assert.False(SvgGeometryService.TryCreateEquivalentPath(shape, SKRect.Create(0f, 0f, 100f, 100f), out var path));
        Assert.Null(path);
    }
}
