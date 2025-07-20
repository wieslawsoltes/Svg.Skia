using System.Drawing;
using System.Linq;
using Svg;
using Svg.Model.Services;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class CssVariableTests : SvgUnitTest
{
    [Fact]
    public void Var_Expression_Is_Resolved()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' style='--c:#00ff00'><rect width='10' height='10' style='fill:var(--c,#ff0000)' /></svg>";
        var doc = SvgService.FromSvg(svg);
        Assert.NotNull(doc);
        var rect = doc!.Children.OfType<SvgRectangle>().First();
        var colorServer = Assert.IsType<SvgColourServer>(rect.Fill);
        Assert.Equal(Color.FromArgb(0, 255, 0).ToArgb(), colorServer.Colour.ToArgb());
    }
}
