using System.Linq;
using Svg;
using Svg.Editor.Core;
using Svg.Editor.Svg;
using Xunit;

namespace Svg.Editor.Svg.UnitTests;

public class FrameServiceTests
{
    [Fact]
    public void GetContainerKind_ReturnsGroupForPlainSvgGroup()
    {
        var group = new SvgGroup { ID = "group-1" };

        Assert.Equal(FrameContainerKind.Group, FrameService.GetContainerKind(group));
        Assert.False(FrameService.IsFrameLikeGroup(group));
    }

    [Fact]
    public void SyncMetadata_DefaultsLegacyContainerToFrame()
    {
        var group = new SvgGroup { ID = "frame-1" };
        group.CustomAttributes[FrameService.FrameAttribute] = "true";
        group.Children.Add(FrameService.CreateBackgroundRect("frame-bg", 32f, 48f, 320f, 240f, FrameContainerKind.Frame));

        FrameService.SyncMetadata(group);

        Assert.Equal(FrameContainerKind.Frame, FrameService.GetContainerKind(group));
        Assert.Equal("320", group.CustomAttributes["width"]);
        Assert.Equal("240", group.CustomAttributes["height"]);
    }

    [Fact]
    public void SetContainerKind_GroupClearsFrameMetadata()
    {
        var group = new SvgGroup { ID = "frame-1" };
        FrameService.SetContainerKind(group, FrameContainerKind.Frame);
        group.CustomAttributes["width"] = "320";
        group.CustomAttributes["height"] = "240";
        FrameService.SetPresetId(group, "desktop");

        FrameService.SetContainerKind(group, FrameContainerKind.Group);

        Assert.False(group.CustomAttributes.ContainsKey(FrameService.FrameAttribute));
        Assert.False(group.CustomAttributes.ContainsKey(FrameService.FrameKindAttribute));
        Assert.False(group.CustomAttributes.ContainsKey(FrameService.FramePresetAttribute));
        Assert.False(group.CustomAttributes.ContainsKey("width"));
        Assert.False(group.CustomAttributes.ContainsKey("height"));
    }

    [Fact]
    public void AutoLayout_IgnoresSectionContainers()
    {
        var autoLayoutService = new AutoLayoutService();
        var document = new SvgDocument();
        var section = new SvgGroup { ID = "section-1" };
        FrameService.SetContainerKind(section, FrameContainerKind.Section);
        section.Children.Add(FrameService.CreateBackgroundRect("section-bg", 0f, 0f, 300f, 200f, FrameContainerKind.Section));
        document.Children.Add(section);

        var content = autoLayoutService.EnsureContentGroup(section);
        content.Children.Add(new SvgRectangle
        {
            ID = "card",
            X = new SvgUnit(SvgUnitType.User, 24f),
            Y = new SvgUnit(SvgUnitType.User, 32f),
            Width = new SvgUnit(SvgUnitType.User, 120f),
            Height = new SvgUnit(SvgUnitType.User, 60f)
        });

        autoLayoutService.WriteSettings(section, new AutoLayoutSettings
        {
            IsEnabled = true,
            ClipContent = true
        });

        var changed = autoLayoutService.ApplyLayout(document, section, static element =>
        {
            return element switch
            {
                SvgRectangle rect => new SkiaSharp.SKRect(rect.X.Value, rect.Y.Value, rect.X.Value + rect.Width.Value, rect.Y.Value + rect.Height.Value),
                _ => null
            };
        });

        Assert.False(changed);
        Assert.Empty(document.Children.OfType<SvgDefinitionList>());
        Assert.Null(content.ClipPath);
    }
}
