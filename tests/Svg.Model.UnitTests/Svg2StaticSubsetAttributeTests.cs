using System.Drawing;
using System.Linq;
using Svg.DataTypes;
using Svg.FilterEffects;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class Svg2StaticSubsetAttributeTests
{
    [Fact]
    public void FromSvg_PreservesDeferredSvg2PaintElementsAsUnknownContent()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <meshgradient id="mesh" gradientUnits="userSpaceOnUse">
                  <meshrow>
                    <meshpatch>
                      <stop path="c 10,0 20,0 30,0" stop-color="#ff0000" />
                    </meshpatch>
                  </meshrow>
                </meshgradient>
                <hatch id="hatch" pitch="4" rotate="45">
                  <hatchpath stroke="#000000" stroke-width="1" />
                </hatch>
                <solidcolor id="solid" solid-color="#123456" solid-opacity="0.5" />
              </defs>
            </svg>
            """);

        var unknownElements = document!
            .Descendants()
            .OfType<SvgUnknownElement>()
            .ToList();

        Assert.Contains(unknownElements, static element => element.ID == "mesh");
        Assert.Contains(unknownElements, static element => element.ID == "hatch");
        Assert.Contains(unknownElements, static element => element.ID == "solid");

        var mesh = Assert.IsType<SvgUnknownElement>(document.GetElementById("mesh"));
        var hatch = Assert.IsType<SvgUnknownElement>(document.GetElementById("hatch"));
        var solid = Assert.IsType<SvgUnknownElement>(document.GetElementById("solid"));

        Assert.Equal("userSpaceOnUse", mesh.CustomAttributes["gradientUnits"]);
        Assert.Equal("4", hatch.CustomAttributes["pitch"]);
        Assert.Equal("#123456", solid.CustomAttributes["solid-color"]);
        Assert.NotEmpty(mesh.Children);
        Assert.NotEmpty(hatch.Children);
    }

    [Fact]
    public void FromSvg_PreservesUnknownSvgElementsAsNonRenderingModelContent()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <unknownStatic id="unknown" data-mode="preserve" customAttr="value">
                <title id="unknown-title">Preserved</title>
              </unknownStatic>
            </svg>
            """);

        var unknown = Assert.IsType<SvgUnknownElement>(document!.GetElementById("unknown"));

        Assert.Equal("preserve", unknown.CustomAttributes["data-mode"]);
        Assert.Equal("value", unknown.CustomAttributes["customAttr"]);
        Assert.Single(unknown.Children);
        Assert.Contains("<unknownStatic", unknown.GetXML(), System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("vector-effect=\"fixed-position\"")]
    [InlineData("style=\"vector-effect: fixed-position\"")]
    [InlineData("vector-effect=\"non-rotation\"")]
    public void FromSvg_UnsupportedVectorEffectValuesFallBackToDefault(string vectorEffect)
    {
        var document = SvgService.FromSvg($$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <path id="shape" d="M0,0 L10,0" stroke="black" {{vectorEffect}} />
            </svg>
            """);

        var shape = Assert.IsType<SvgPath>(document!.GetElementById("shape"));
        shape.FlushStyles();

        Assert.Equal(SvgVectorEffect.None, shape.VectorEffect);
    }

    [Fact]
    public void FromSvg_StrokeLinejoinArcsIsPreservedForCompatibility()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <path id="shape" d="M0,0 L10,10 L20,0" stroke="black" stroke-linejoin="arcs" />
            </svg>
            """);

        var shape = Assert.IsType<SvgPath>(document!.GetElementById("shape"));
        shape.FlushStyles();

        Assert.Equal(SvgStrokeLineJoin.Arcs, shape.StrokeLineJoin);
    }

    [Fact]
    public void FromSvg_DynamicInteractiveContentIsPreservedButDoesNotMutateStaticBaseValues()
    {
        var parameters = new SvgParameters(
            null,
            null,
            null,
            new SvgDocumentLoadOptions { ProcessingMode = SvgProcessingMode.DynamicInteractive });

        var parsedDocument = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <script>document.getElementById('shape').setAttribute('fill', 'blue');</script>
              <rect id="shape" x="0" y="0" width="10" height="10" fill="red" onclick="activate()">
                <animate id="move" attributeName="x" from="0" to="50" dur="1s" fill="freeze" />
              </rect>
            </svg>
            """, parameters);
        Assert.NotNull(parsedDocument);

        var document = parsedDocument!;
        var options = SvgService.GetDocumentLoadOptions(document);
        var shape = Assert.IsType<SvgRectangle>(document.GetElementById("shape"));
        var fill = Assert.IsType<SvgColourServer>(shape.Fill);

        Assert.Equal(SvgProcessingMode.DynamicInteractive, options.ProcessingMode);
        Assert.Single(document.Descendants().OfType<SvgScript>());
        Assert.IsType<SvgAnimate>(document.GetElementById("move"));
        Assert.Equal(0f, shape.X.Value);
        Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        Assert.Equal("activate()", shape.CustomAttributes["onclick"]);
    }

    [Fact]
    public void FromSvg_ParsesMarkerAutoStartReverseOrient()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <marker id="arrow" orient="auto-start-reverse" markerWidth="10" markerHeight="10" refX="5" refY="5">
                  <path d="M 0 0 L 10 5 L 0 10 z" />
                </marker>
              </defs>
            </svg>
            """);

        var marker = Assert.IsType<SvgMarker>(document!.GetElementById("arrow"));

        Assert.True(marker.Orient.IsAuto);
        Assert.True(marker.Orient.IsAutoStartReverse);
        Assert.Equal("auto-start-reverse", marker.Orient.ToString());
    }

    [Fact]
    public void SvgOrientConverter_PreservesAutoStartReverseModelValue()
    {
        var converter = new SvgOrientConverter();

        var orient = Assert.IsType<SvgOrient>(converter.ConvertFrom("auto-start-reverse"));

        Assert.True(orient.IsAuto);
        Assert.True(orient.IsAutoStartReverse);
        Assert.Equal("auto-start-reverse", orient.ToString());
    }

    [Fact]
    public void FromSvg_ParsesSvg2StaticSubsetScaffoldAttributes()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <symbol id="icon" x="1" y="2" width="24" height="25" refX="3" refY="4" viewBox="0 0 24 24">
                  <circle cx="12" cy="12" r="10" />
                </symbol>
                <filter id="dropFilter">
                  <feDropShadow id="dropShadow" in="SourceGraphic" result="shadow" dx="4" dy="5" stdDeviation="6 7" flood-color="#123456" flood-opacity="0.5" />
                </filter>
              </defs>
              <circle id="circle" cx="10" cy="10" r="5" pathLength="42" />
              <rect id="rect" x="1" y="1" width="10" height="10" pathLength="43" />
            </svg>
            """);

        var symbol = Assert.IsType<SvgSymbol>(document!.GetElementById("icon"));
        Assert.Equal(1f, symbol.X.Value);
        Assert.Equal(2f, symbol.Y.Value);
        Assert.Equal(24f, symbol.Width.Value);
        Assert.Equal(25f, symbol.Height.Value);
        Assert.Equal(3f, symbol.RefX.Value);
        Assert.Equal(4f, symbol.RefY.Value);

        var circle = Assert.IsType<SvgCircle>(document.GetElementById("circle"));
        var rect = Assert.IsType<SvgRectangle>(document.GetElementById("rect"));
        Assert.Equal(42f, circle.PathLength);
        Assert.Equal(43f, rect.PathLength);

        var dropShadow = Assert.IsType<SvgDropShadow>(document.GetElementById("dropShadow"));
        Assert.Equal("SourceGraphic", dropShadow.Input);
        Assert.Equal("shadow", dropShadow.Result);
        Assert.Equal(4f, dropShadow.Dx.Value);
        Assert.Equal(5f, dropShadow.Dy.Value);
        Assert.Equal(new[] { 6f, 7f }, dropShadow.StdDeviation);
        Assert.Equal(0.5f, dropShadow.FloodOpacity);
    }

    [Fact]
    public void SvgDropShadow_DeepCopyPreservesExplicitOffsets()
    {
        var dropShadow = new SvgDropShadow
        {
            Dx = 4f,
            Dy = 5f
        };

        var copy = Assert.IsType<SvgDropShadow>(dropShadow.DeepCopy());

        Assert.Equal(4f, copy.Dx.Value);
        Assert.Equal(5f, copy.Dy.Value);
    }

    [Fact]
    public void SvgDropShadow_DeepCopyPreservesParsedOffsets()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <filter id="shadow">
                <feDropShadow id="dropShadow" dx="6" dy="7" />
              </filter>
            </svg>
            """);

        var dropShadow = Assert.IsType<SvgDropShadow>(document!.GetElementById("dropShadow"));

        var copy = Assert.IsType<SvgDropShadow>(dropShadow.DeepCopy());

        Assert.Equal(6f, copy.Dx.Value);
        Assert.Equal(7f, copy.Dy.Value);
    }
}
