using System;
using System.Globalization;
using System.Linq;
using System.Xml;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgWorkspaceLayoutTests
{
    [Theory]
    [InlineData(SvgWorkspacePreset.Essentials)]
    [InlineData(SvgWorkspacePreset.Drawing)]
    [InlineData(SvgWorkspacePreset.Typography)]
    public void PresetsContainEachPanelExactlyOnce(SvgWorkspacePreset preset)
    {
        var layout = new SvgWorkspaceLayout(); layout.Reset(preset);
        Assert.Equal(SvgWorkspaceLayout.PanelIds.Count, layout.Panels.Count);
        Assert.Equal(layout.Panels.Count, layout.Panels.Select(panel => panel.Id).Distinct().Count());
        Assert.All(SvgWorkspaceLayout.PanelIds, id => Assert.NotNull(layout.Find(id)));
        Assert.NotNull(layout.Active(SvgPanelDock.RightTop));
    }
    [Fact]
    public void MovingActivePanelSelectsItAtDestinationAndRepairsOldGroup()
    {
        var layout = new SvgWorkspaceLayout();
        layout.Move("properties", SvgPanelDock.Left);
        Assert.Equal("properties", layout.Active(SvgPanelDock.Left));
        Assert.NotEqual("properties", layout.Active(SvgPanelDock.RightTop));
        Assert.Equal(SvgPanelDock.Left, layout.Find("properties").Dock);
    }
    [Fact]
    public void TabReorderingDoesNotDuplicateOrLoseContentIdentity()
    {
        var layout = new SvgWorkspaceLayout();
        var panel = layout.Find("appearance");
        layout.Move("appearance", SvgPanelDock.RightTop, 0);
        Assert.Equal("appearance", layout.Panels.First(item => item.Dock == SvgPanelDock.RightTop).Id);
        layout.Move("appearance", SvgPanelDock.RightTop, 2);
        Assert.Same(panel, layout.Panels.Where(item => item.Dock == SvgPanelDock.RightTop).ElementAt(2));
        Assert.Equal(SvgWorkspaceLayout.PanelIds.Count, layout.Panels.Count);
    }
    [Fact]
    public void ClosedPanelCanBeReopenedWithoutLosingFloatingGeometry()
    {
        var layout = new SvgWorkspaceLayout();
        layout.SetFloatingBounds("appearance", 100, 80, 340, 400, 1440, 900);
        layout.Move("appearance", SvgPanelDock.Hidden);
        layout.Activate("appearance");
        Assert.Equal(SvgPanelDock.RightTop, layout.Find("appearance").Dock);
        Assert.Equal(340, layout.Find("appearance").Width);
        Assert.Equal("appearance", layout.Active(SvgPanelDock.RightTop));
    }
    [Fact]
    public void LayoutRoundTripPreservesOrderTabsAndExtents()
    {
        var layout = new SvgWorkspaceLayout();
        layout.Reset(SvgWorkspacePreset.Drawing);
        layout.Move("effects", SvgPanelDock.Bottom);
        layout.Move("stroke", SvgPanelDock.Floating);
        layout.SetFloatingBounds("stroke", 90, 110, 370, 410, 1440, 900);
        layout.Resize(SvgPanelDock.Left, 300); layout.Resize(SvgPanelDock.RightTop, 345);
        layout.Resize(SvgPanelDock.Bottom, 260); layout.SetRightSplit(0.55);
        layout.Activate("appearance"); layout.TouchDensity = true; layout.ShowRulers = false;
        var restored = SvgWorkspaceLayout.Parse(layout.Serialize());
        Assert.Equal(layout.Serialize(), restored.Serialize());
    }
    [Fact]
    public void LayoutIsCultureInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            var layout = new SvgWorkspaceLayout(); layout.Resize(SvgPanelDock.Left, 250.25); layout.SetRightSplit(0.625);
            var restored = SvgWorkspaceLayout.Parse(layout.Serialize());
            Assert.Equal(250.25, restored.LeftWidth); Assert.Equal(0.625, restored.RightTopRatio);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public void UnsupportedVersionsAndUnknownPanelsFallBackSafely()
    {
        var future = SvgWorkspaceLayout.Parse("<workspace version='999'><panel id='properties' dock='Hidden'/></workspace>");
        Assert.Equal(SvgPanelDock.RightTop, future.Find("properties").Dock);
        var partial = SvgWorkspaceLayout.Parse("<workspace version='1' left='NaN' right='999999'><panel id='unknown' dock='Left'/><panel id='layers' dock='Left'/><panel id='layers' dock='Bottom'/><panel id='effects' dock='999'/></workspace>");
        Assert.Equal(248, partial.LeftWidth); Assert.Equal(640, partial.RightWidth);
        Assert.Equal(SvgPanelDock.Left, partial.Find("layers").Dock);
        Assert.Equal(SvgPanelDock.RightTop, partial.Find("effects").Dock);
        Assert.Equal(SvgWorkspaceLayout.PanelIds.Count, partial.Panels.Count);
    }
    [Fact]
    public void SettingsRejectEntitiesAndUnboundedInput()
    {
        Assert.Throws<XmlException>(() => SvgWorkspaceLayout.Parse("<!DOCTYPE workspace [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><workspace version='1'>&x;</workspace>"));
        Assert.Throws<ArgumentException>(() => SvgWorkspaceLayout.Parse(new string('x', 32769)));
    }
    [Fact]
    public void FloatingPanelsStayWithinResizedViewport()
    {
        var layout = new SvgWorkspaceLayout();
        layout.SetFloatingBounds("layers", 2000, -30, 800, 900, 390, 600);
        var panel = layout.Find("layers");
        Assert.Equal(0, panel.X); Assert.Equal(0, panel.Y);
        Assert.Equal(390, panel.Width); Assert.Equal(600, panel.Height);
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.SetFloatingBounds("layers", double.NaN, 0, 300, 300, 800, 600));
    }
    [Theory]
    [InlineData(390, 844, 0, 0)]
    [InlineData(800, 600, 0, 0)]
    [InlineData(1100, 800, 0, 312)]
    [InlineData(1440, 960, 248, 312)]
    public void ResponsiveDocksRetainCanvasRoom(double width, double height, double left, double right)
    {
        var layout = new SvgWorkspaceLayout(); layout.Reset(SvgWorkspacePreset.Drawing);
        var metrics = layout.Measure(width, height);
        Assert.Equal(left, metrics.LeftWidth); Assert.Equal(right, metrics.RightWidth);
        Assert.True(width - metrics.LeftWidth - metrics.RightWidth > 250);
        Assert.Equal(248, layout.LeftWidth); Assert.Equal(312, layout.RightWidth);
    }
    [Fact]
    public void FocusAndTemporaryHideDoNotChangePersistedLayout()
    {
        var layout = new SvgWorkspaceLayout(); var saved = layout.Serialize();
        Assert.Equal(0, layout.Measure(1440, 960, true).RightWidth);
        layout.PanelsHidden = true;
        Assert.Equal(0, layout.Measure(1440, 960).RightWidth);
        Assert.Equal(saved, layout.Serialize());
    }
    [Theory]
    [InlineData(0.01)] [InlineData(0.1)] [InlineData(1)] [InlineData(8)] [InlineData(128)]
    public void RulersRemainOrderedBoundedAndAlignedAtEverySupportedZoom(double scale)
    {
        var ticks = SvgRulerMetrics.Create(137, scale, 1200);
        Assert.InRange(ticks.Count, 1, 512);
        Assert.Contains(ticks, tick => tick.IsMajor);
        Assert.All(ticks, tick =>
        {
            Assert.InRange(tick.Pixel, 0, 1200);
            Assert.Equal(137 + tick.Value * scale, tick.Pixel, 7);
        });
        Assert.True(ticks.Zip(ticks.Skip(1), (first, second) => first.Pixel < second.Pixel).All(value => value));
    }
    [Fact]
    public void RulersRejectInvalidAndUnrepresentableCameraValues()
    {
        Assert.Empty(SvgRulerMetrics.Create(double.NaN, 1, 800));
        Assert.Empty(SvgRulerMetrics.Create(0, 0, 800));
        Assert.Empty(SvgRulerMetrics.Create(0, 1, double.PositiveInfinity));
        Assert.Empty(SvgRulerMetrics.Create(double.MaxValue, 0.01, 800));
    }
    [Fact]
    public void WorkspaceEditsNeverEnterDocumentHistory()
    {
        using var controller = new SvgEditorController(); controller.NewDocument();
        var source = controller.GetSvg(); var picture = controller.Renderer.Picture;
        var layout = new SvgWorkspaceLayout(); layout.Move("layers", SvgPanelDock.Bottom); layout.Resize(SvgPanelDock.Bottom, 300);
        Assert.Equal(source, controller.GetSvg()); Assert.Same(picture, controller.Renderer.Picture); Assert.False(controller.CanUndo);
    }
}
