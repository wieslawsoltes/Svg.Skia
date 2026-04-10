using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class PointerEventsTests
{
    [Fact]
    public void FromSvg_ParsesAndInheritsPointerEvents()
    {
        var document = SvgService.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <g id="group" pointer-events="visibleStroke">
                <rect id="child" x="10" y="10" width="50" height="50" />
              </g>
            </svg>
            """);

        var group = Assert.IsType<SvgGroup>(document!.GetElementById("group"));
        var child = Assert.IsType<SvgRectangle>(document.GetElementById("child"));

        Assert.Equal(SvgPointerEvents.VisibleStroke, group.PointerEvents);
        Assert.Equal(SvgPointerEvents.VisibleStroke, child.PointerEvents);
    }
}
