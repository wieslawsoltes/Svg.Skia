using System.ComponentModel;
using System.Drawing;
using ShimSkiaSharp;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class Svg2StaticStyleContractTests
{
    [Fact]
    public void SvgWhiteSpaceConverter_FormatsNoWrapAsCssKeyword()
    {
        var converter = TypeDescriptor.GetConverter(typeof(SvgWhiteSpace));

        Assert.Equal("nowrap", converter.ConvertToInvariantString(SvgWhiteSpace.NoWrap));
        Assert.Equal(SvgWhiteSpace.NoWrap, converter.ConvertFromInvariantString("nowrap"));
        Assert.Equal(SvgWhiteSpace.NoWrap, converter.ConvertFromInvariantString("no-wrap"));
    }

    [Theory]
    [InlineData("collapse wrap", SvgWhiteSpace.Normal)]
    [InlineData("collapse nowrap", SvgWhiteSpace.NoWrap)]
    [InlineData("preserve nowrap", SvgWhiteSpace.Pre)]
    [InlineData("preserve wrap", SvgWhiteSpace.PreWrap)]
    [InlineData("preserve-breaks wrap", SvgWhiteSpace.PreLine)]
    [InlineData("break-spaces wrap", SvgWhiteSpace.BreakSpaces)]
    public void ComputedStyle_ParsesCssText4WhiteSpaceShorthand(string whiteSpace, SvgWhiteSpace expected)
    {
        var document = SvgService.FromSvg(
            $$"""
              <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
                <text id="label" style="white-space: {{whiteSpace}}">hello</text>
              </svg>
              """,
            null);

        var label = Assert.IsType<SvgText>(document!.GetElementById("label"));
        label.FlushStyles();

        Assert.Equal(expected, label.WhiteSpace);
    }

    [Fact]
    public void ComputedStyle_DerivesCssText4WhiteSpaceLonghandsFromShorthand()
    {
        var document = SvgService.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <text id="label" style="white-space: preserve-spaces wrap discard-before discard-after">hello</text>
            </svg>
            """,
            null);

        var label = Assert.IsType<SvgText>(document!.GetElementById("label"));
        label.FlushStyles();

        Assert.Equal(SvgWhiteSpace.Normal, label.WhiteSpace);
        Assert.Equal("preserve-spaces", label.WhiteSpaceCollapse);
        Assert.Equal("wrap", label.TextWrapMode);
        Assert.Equal("discard-before discard-after", label.WhiteSpaceTrim);
    }

    [Theory]
    [InlineData("collapse", "nowrap", SvgWhiteSpace.NoWrap)]
    [InlineData("collapse", "wrap", SvgWhiteSpace.Normal)]
    [InlineData("preserve", "nowrap", SvgWhiteSpace.Pre)]
    [InlineData("preserve", "wrap", SvgWhiteSpace.PreWrap)]
    [InlineData("preserve-breaks", "wrap", SvgWhiteSpace.PreLine)]
    [InlineData("break-spaces", "wrap", SvgWhiteSpace.BreakSpaces)]
    public void ComputedStyle_DerivesWhiteSpaceFromCssText4Longhands(
        string whiteSpaceCollapse,
        string textWrapMode,
        SvgWhiteSpace expected)
    {
        var document = SvgService.FromSvg(
            $$"""
              <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
                <text id="label"
                      style="white-space-collapse: {{whiteSpaceCollapse}}; text-wrap-mode: {{textWrapMode}}">hello</text>
              </svg>
              """,
            null);

        var label = Assert.IsType<SvgText>(document!.GetElementById("label"));
        label.FlushStyles();

        Assert.Equal(expected, label.WhiteSpace);
        Assert.Equal(whiteSpaceCollapse, label.WhiteSpaceCollapse);
        Assert.Equal(textWrapMode, label.TextWrapMode);
        Assert.Equal("none", label.WhiteSpaceTrim);
    }

    [Fact]
    public void ComputedStyle_CssText4WhiteSpaceLonghandsOverridePresentationAttribute()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <style>
                #label {
                  white-space-collapse: preserve;
                  text-wrap-mode: nowrap;
                }
              </style>
              <text id="label" white-space="normal">hello</text>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var label = Assert.IsType<SvgText>(document!.GetElementById("label"));
        label.FlushStyles();

        Assert.Equal(SvgWhiteSpace.Pre, label.WhiteSpace);
        Assert.Equal("preserve", label.WhiteSpaceCollapse);
        Assert.Equal("nowrap", label.TextWrapMode);
    }

    [Fact]
    public void ComputedStyle_UnsupportedWhiteSpaceLonghandCombinationKeepsFallback()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <text id="label"
                    white-space="pre-wrap"
                    style="white-space-collapse: break-spaces; text-wrap-mode: nowrap">hello</text>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var label = Assert.IsType<SvgText>(document!.GetElementById("label"));
        label.FlushStyles();

        Assert.Equal(SvgWhiteSpace.PreWrap, label.WhiteSpace);
    }

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
                    white-space-collapse="preserve"
                    text-wrap-mode="wrap"
                    white-space-trim="discard-before discard-after"
                    text-overflow="ellipsis"
                    font-feature-settings="'liga' 0, 'kern' 1"
                    font-kerning="none"
                    font-variant-ligatures="no-common-ligatures discretionary-ligatures"
                    inline-size="32px"
                    shape-inside="url(#shape)"
                    shape-subtract="none"
                    shape-image-threshold="0.5">hello</text>
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
        Assert.Equal("preserve", label.WhiteSpaceCollapse);
        Assert.Equal("wrap", label.TextWrapMode);
        Assert.Equal("discard-before discard-after", label.WhiteSpaceTrim);
        Assert.Equal("ellipsis", label.TextOverflow);
        Assert.Equal("'liga' 0, 'kern' 1", label.FontFeatureSettings);
        Assert.Equal("none", label.FontKerning);
        Assert.Equal("no-common-ligatures discretionary-ligatures", label.FontVariantLigatures);
        Assert.Equal("32px", label.InlineSize);
        Assert.Equal("url(#shape)", label.ShapeInside);
        Assert.Equal("none", label.ShapeSubtract);
        Assert.Equal("0.5", label.ShapeImageThreshold);
        Assert.NotNull(pathText.PathData);
        Assert.Equal(2, pathText.PathData.Count);
        Assert.Equal(SvgTextPathSide.Right, pathText.Side);
    }

    [Fact]
    public void Svg11AltGlyphElements_ArePreservedInModel()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="100" height="100">
              <defs>
                <altGlyphDef id="alt">
                  <altGlyphItem id="item">
                    <glyphRef id="ref" xlink:href="#glyphA" glyphRef="glyphA" format="svg" x="1" y="2" dx="3" dy="4" />
                  </altGlyphItem>
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="20">
                <altGlyph id="ag" xlink:href="#alt" glyphRef="glyphA" format="svg">A</altGlyph>
              </text>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);

        var altGlyphDef = Assert.IsType<SvgAltGlyphDef>(document!.GetElementById("alt"));
        var altGlyphItem = Assert.IsType<SvgAltGlyphItem>(document.GetElementById("item"));
        var glyphRef = Assert.IsType<SvgGlyphRef>(document.GetElementById("ref"));
        var altGlyph = Assert.IsType<SvgAltGlyph>(document.GetElementById("ag"));

        Assert.Single(altGlyphDef.Children);
        Assert.Single(altGlyphItem.Children);
        Assert.Equal("#glyphA", glyphRef.ReferencedElement.ToString());
        Assert.Equal("glyphA", glyphRef.GlyphRef);
        Assert.Equal("svg", glyphRef.Format);
        Assert.Equal(1f, glyphRef.X.Value);
        Assert.Equal(2f, glyphRef.Y.Value);
        Assert.Equal(3f, glyphRef.Dx.Value);
        Assert.Equal(4f, glyphRef.Dy.Value);
        Assert.Equal("#alt", altGlyph.ReferencedElement.ToString());
        Assert.Equal("glyphA", altGlyph.GlyphRef);
        Assert.Equal("svg", altGlyph.Format);
        Assert.Equal("A", altGlyph.Text);
    }

    [Theory]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg"><rect id="target" fill="currentColor" /></svg>""")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" color="currentColor"><rect id="target" fill="currentColor" /></svg>""")]
    public void PaintServer_CurrentColorWithoutConcreteAncestorResolvesToInitialBlack(string svg)
    {
        var document = SvgService.FromSvg(svg, null);
        var rect = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var fill = Assert.IsType<SvgDeferredPaintServer>(rect.Fill);

        var resolved = Assert.IsType<SvgColourServer>(SvgDeferredPaintServer.TryGet<SvgPaintServer>(fill, rect));

        Assert.Equal(Color.Black.ToArgb(), resolved.Colour.ToArgb());
    }

    [Fact]
    public void PaintServer_ParsesLegacyIccColorFallbackAsSrgbColor()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <defs>
                <linearGradient id="lg1">
                  <stop offset="0" stop-color="white" />
                  <stop offset="1" stop-color="black" />
                </linearGradient>
              </defs>
              <rect id="target" fill="url(#lg1) green icc-color(acmecmyk, 0.11, 0.48, 0.83, 0.00)" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var rect = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));
        var fill = Assert.IsType<SvgDeferredPaintServer>(rect.Fill);
        var fallback = Assert.IsType<SvgColourServer>(fill.FallbackServer);

        Assert.Equal(Color.Green.ToArgb(), fallback.Colour.ToArgb());
    }

    [Fact]
    public void PaintOpacity_PercentageValuesNormalizeToUnitOpacity()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" style="opacity: 0.1%">
              <g fill-opacity="0.3" stroke-opacity="0.4">
                <rect id="target"
                      width="10"
                      height="10"
                      fill-opacity="50%"
                      stroke-opacity="25%" />
              </g>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var rect = Assert.IsType<SvgRectangle>(document!.GetElementById("target"));

        Assert.Equal(0.001f, document.Opacity, 3);
        Assert.Equal(0.5f, rect.FillOpacity, 3);
        Assert.Equal(0.25f, rect.StrokeOpacity, 3);
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
                  font-feature-settings: "liga" 0, "kern" 1;
                  font-kerning: none;
                  font-variant-ligatures: no-common-ligatures discretionary-ligatures;
                  inline-size: 48px;
                  shape-inside: url(#shape);
                  shape-subtract: none;
                  shape-image-threshold: 0.75;
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
        Assert.Equal("\"liga\" 0, \"kern\" 1", label.FontFeatureSettings);
        Assert.Equal("none", label.FontKerning);
        Assert.Equal("no-common-ligatures discretionary-ligatures", label.FontVariantLigatures);
        Assert.Equal("48px", label.InlineSize);
        Assert.Equal("url(\"#shape\")", label.ShapeInside);
        Assert.Equal("none", label.ShapeSubtract);
        Assert.Equal("0.75", label.ShapeImageThreshold);
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
    public void ComputedStyle_InvalidLineHeightStyleDoesNotOverridePositiveAttributeFallback()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <text id="label" line-height="1.5" style="line-height:0">hello</text>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var label = Assert.IsType<SvgText>(document!.GetElementById("label"));

        label.FlushStyles();

        Assert.Equal("1.5", label.LineHeight);
    }

    [Fact]
    public void ComputedStyle_UppercaseLineHeightStyleIsRecognized()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <text id="label" style="LINE-HEIGHT:2">hello</text>
            </svg>
            """;

        var document = SvgService.FromSvg(svg, null);
        var label = Assert.IsType<SvgText>(document!.GetElementById("label"));

        label.FlushStyles();

        Assert.Equal("2", label.LineHeight);
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
                    font-feature-settings="'liga' 0"
                    font-kerning="normal"
                    font-variant-ligatures="common-ligatures"
                    inline-size="32px"
                    style="white-space: sideways; text-overflow: overflow; font-feature-settings: 'too-long' 1; font-kerning: sideways; font-variant-ligatures: made-up; inline-size: definitely-not-a-length">hello</text>
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
        Assert.Equal("'liga' 0", label.FontFeatureSettings);
        Assert.Equal("normal", label.FontKerning);
        Assert.Equal("common-ligatures", label.FontVariantLigatures);
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
