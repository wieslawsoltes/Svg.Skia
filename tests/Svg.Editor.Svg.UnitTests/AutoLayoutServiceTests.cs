using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Core;
using Svg.Editor.Svg;
using Svg.Transforms;
using Xunit;

namespace Svg.Editor.Svg.UnitTests;

public class AutoLayoutServiceTests
{
    [Fact]
    public void EnsureContentGroup_MovesFrameChildrenUnderHiddenWrapper()
    {
        var service = new AutoLayoutService();
        var frame = CreateFrame(24f, 32f, 240f, 180f);
        var rect = CreateRect("card", 40f, 60f, 80f, 40f);
        frame.Children.Add(rect);

        var content = service.EnsureContentGroup(frame);

        Assert.True(service.IsFrameContentGroup(content));
        Assert.Contains(rect, content.Children);
        Assert.DoesNotContain(rect, frame.Children);
        Assert.Equal(2, frame.Children.Count);
    }

    [Fact]
    public void ReadAndWriteSettings_RoundTripsFrameMetadata()
    {
        var service = new AutoLayoutService();
        var frame = CreateFrame(0f, 0f, 320f, 240f);
        var settings = new AutoLayoutSettings
        {
            IsEnabled = true,
            Flow = AutoLayoutFlow.Wrap,
            WidthMode = AutoLayoutSizeMode.Hug,
            HeightMode = AutoLayoutSizeMode.Fixed,
            HorizontalAlignment = AutoLayoutAlignment.Center,
            VerticalAlignment = AutoLayoutAlignment.End,
            Gap = 18f,
            PaddingHorizontal = 20f,
            PaddingVertical = 16f,
            ClipContent = true
        };

        service.WriteSettings(frame, settings);
        var reloaded = service.ReadSettings(frame);

        Assert.True(reloaded.IsEnabled);
        Assert.Equal(AutoLayoutFlow.Wrap, reloaded.Flow);
        Assert.Equal(AutoLayoutSizeMode.Hug, reloaded.WidthMode);
        Assert.Equal(AutoLayoutSizeMode.Fixed, reloaded.HeightMode);
        Assert.Equal(AutoLayoutAlignment.Center, reloaded.HorizontalAlignment);
        Assert.Equal(AutoLayoutAlignment.End, reloaded.VerticalAlignment);
        Assert.Equal(18f, reloaded.Gap);
        Assert.Equal(20f, reloaded.PaddingHorizontal);
        Assert.Equal(16f, reloaded.PaddingVertical);
        Assert.True(reloaded.ClipContent);
    }

    [Fact]
    public void ApplyLayout_HorizontalFlowPositionsChildrenAndCreatesClipPath()
    {
        var service = new AutoLayoutService();
        var document = new SvgDocument();
        var frame = CreateFrame(100f, 50f, 300f, 200f);
        document.Children.Add(frame);

        var content = service.EnsureContentGroup(frame);
        var first = CreateRect("first", 0f, 0f, 40f, 20f);
        var second = CreateRect("second", 80f, 80f, 30f, 30f);
        content.Children.Add(first);
        content.Children.Add(second);

        service.WriteSettings(frame, new AutoLayoutSettings
        {
            IsEnabled = true,
            Flow = AutoLayoutFlow.Horizontal,
            Gap = 10f,
            PaddingHorizontal = 20f,
            PaddingVertical = 15f,
            ClipContent = true
        });

        var changed = service.ApplyLayout(document, frame, GetBounds);

        Assert.True(changed);
        Assert.Equal((120f, 65f), GetTranslation(first));
        Assert.Equal((90f, -15f), GetTranslation(second));

        var defs = Assert.Single(document.Children.OfType<SvgDefinitionList>());
        var clipPath = Assert.Single(defs.Children.OfType<SvgClipPath>());
        var clipRect = Assert.Single(clipPath.Children.OfType<SvgRectangle>());
        Assert.Equal(300f, clipRect.Width.Value);
        Assert.Equal(200f, clipRect.Height.Value);
        Assert.Equal($"#{clipPath.ID}", content.ClipPath?.OriginalString);
    }

    [Fact]
    public void ApplyLayout_HugModesResizeFrameToContent()
    {
        var service = new AutoLayoutService();
        var document = new SvgDocument();
        var frame = CreateFrame(0f, 0f, 320f, 200f);
        document.Children.Add(frame);

        var content = service.EnsureContentGroup(frame);
        content.Children.Add(CreateRect("one", 0f, 0f, 50f, 20f));
        content.Children.Add(CreateRect("two", 0f, 0f, 30f, 30f));

        service.WriteSettings(frame, new AutoLayoutSettings
        {
            IsEnabled = true,
            Flow = AutoLayoutFlow.Vertical,
            WidthMode = AutoLayoutSizeMode.Hug,
            HeightMode = AutoLayoutSizeMode.Hug,
            Gap = 10f,
            PaddingHorizontal = 12f,
            PaddingVertical = 8f
        });

        service.ApplyLayout(document, frame, GetBounds);

        Assert.True(service.TryGetFrameBackground(frame, out var background));
        Assert.Equal(74f, background.Width.Value, 2);
        Assert.Equal(76f, background.Height.Value, 2);
    }

    private static SvgGroup CreateFrame(float x, float y, float width, float height)
    {
        var frame = new SvgGroup
        {
            ID = "frame-1"
        };
        frame.CustomAttributes["data-frame"] = "true";
        frame.CustomAttributes["width"] = width.ToString(System.Globalization.CultureInfo.InvariantCulture);
        frame.CustomAttributes["height"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture);
        frame.Children.Add(new SvgRectangle
        {
            ID = "frame-bg",
            X = new SvgUnit(SvgUnitType.User, x),
            Y = new SvgUnit(SvgUnitType.User, y),
            Width = new SvgUnit(SvgUnitType.User, width),
            Height = new SvgUnit(SvgUnitType.User, height)
        });
        ((SvgRectangle)frame.Children[0]).CustomAttributes["data-frame-bg"] = "true";
        return frame;
    }

    private static SvgRectangle CreateRect(string id, float x, float y, float width, float height)
    {
        return new SvgRectangle
        {
            ID = id,
            X = new SvgUnit(SvgUnitType.User, x),
            Y = new SvgUnit(SvgUnitType.User, y),
            Width = new SvgUnit(SvgUnitType.User, width),
            Height = new SvgUnit(SvgUnitType.User, height)
        };
    }

    private static SKRect? GetBounds(SvgVisualElement element)
    {
        var (tx, ty) = GetTranslation(element);
        return element switch
        {
            SvgRectangle rect => new SKRect(
                rect.X.Value + tx,
                rect.Y.Value + ty,
                rect.X.Value + tx + rect.Width.Value,
                rect.Y.Value + ty + rect.Height.Value),
            _ => null
        };
    }

    private static (float X, float Y) GetTranslation(SvgVisualElement element)
    {
        var translate = element.Transforms?.OfType<SvgTranslate>().FirstOrDefault();
        return translate is null ? (0f, 0f) : (translate.X, translate.Y);
    }
}
