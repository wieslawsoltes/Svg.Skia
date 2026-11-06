using System;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgWorkspaceFloatingBoundsTests
{
    [Fact]
    public void CompactProjectionDoesNotOverwritePreferredLayout()
    {
        var layout = new SvgWorkspaceLayout();
        layout.Move("appearance", SvgPanelDock.Floating);
        layout.SetFloatingBounds("appearance", 800, 400, 480, 600, 1600, 1200);
        var saved = layout.Serialize();
        var panel = layout.Find("appearance");
        var compact = SvgWorkspaceFloatingBounds.Project(panel, 378, 500);
        Assert.Equal(0, compact.X); Assert.Equal(0, compact.Y);
        Assert.Equal(378, compact.Width); Assert.Equal(500, compact.Height);
        Assert.Equal(saved, layout.Serialize());
        var restored = SvgWorkspaceFloatingBounds.Project(panel, 1600, 1200);
        Assert.Equal(800, restored.X); Assert.Equal(400, restored.Y);
        Assert.Equal(480, restored.Width); Assert.Equal(600, restored.Height);
    }

    [Theory]
    [InlineData(200, 100)]
    [InlineData(390, 844)]
    [InlineData(1440, 960)]
    public void TheEntireProjectedPanelFitsInsideTheAvailableSurface(double width, double height)
    {
        var panel = new SvgWorkspaceLayout().Find("layers");
        var bounds = SvgWorkspaceFloatingBounds.Project(panel, width, height);
        Assert.InRange(bounds.X, 0, width);
        Assert.InRange(bounds.Y, 0, height);
        Assert.InRange(bounds.X + bounds.Width, 0, width);
        Assert.InRange(bounds.Y + bounds.Height, 0, height);
    }

    [Fact]
    public void ZeroSizeLayoutIsRepresentableWithoutChangingPreferences()
    {
        var panel = new SvgWorkspaceLayout().Find("layers");
        var bounds = SvgWorkspaceFloatingBounds.Project(panel, 0, 0);
        Assert.Equal(0, bounds.Width); Assert.Equal(0, bounds.Height);
        Assert.Equal(320, panel.Width); Assert.Equal(440, panel.Height);
    }

    [Fact]
    public void InvalidViewportsAreRejected()
    {
        var panel = new SvgWorkspaceLayout().Find("layers");
        Assert.Throws<ArgumentNullException>(() => SvgWorkspaceFloatingBounds.Project(null!, 800, 600));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgWorkspaceFloatingBounds.Project(panel, double.NaN, 600));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgWorkspaceFloatingBounds.Project(panel, 800, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgWorkspaceFloatingBounds.Project(panel, -1, 600));
    }
}
