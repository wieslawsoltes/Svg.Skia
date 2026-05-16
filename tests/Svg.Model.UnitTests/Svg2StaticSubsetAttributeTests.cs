using Svg.DataTypes;
using Svg.FilterEffects;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class Svg2StaticSubsetAttributeTests
{
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
