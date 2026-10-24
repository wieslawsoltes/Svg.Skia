using System;
using SkiaSharp;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorTouchNavigationTests
{
    [Fact]
    public void SingleContactRemainsAvailableForDrawing()
    {
        var navigation = new SvgEditorTouchNavigation();
        var viewport = new SvgEditorViewport();
        Assert.False(navigation.AddContact(10, new SKPoint(20, 30)));
        Assert.False(navigation.MoveContact(10, new SKPoint(60, 70), viewport));
        Assert.False(navigation.IsNavigating);
        Assert.Equal(1, viewport.Zoom);
        Assert.Equal(0, viewport.OffsetX);
        Assert.Equal(0, viewport.OffsetY);
        Assert.False(navigation.RemoveContact(10));
        Assert.Equal(0, navigation.ContactCount);
    }

    [Fact]
    public void PinchKeepsWorldPointUnderMovingMidpointInOneCameraUpdate()
    {
        var navigation = new SvgEditorTouchNavigation();
        var viewport = new SvgEditorViewport();
        viewport.Pan(20, -30);
        viewport.SetZoomAt(2, new SKPoint(50, 50));
        navigation.AddContact(10, new SKPoint(100, 100));
        Assert.True(navigation.AddContact(20, new SKPoint(200, 100)));
        var world = viewport.ViewToWorld(new SKPoint(150, 100));
        var updates = 0;
        viewport.Changed += (_, _) => updates++;

        Assert.True(navigation.MoveContact(10, new SKPoint(80, 120), viewport));

        Assert.Equal(1, updates);
        Assert.Equal(2 * Math.Sqrt(120 * 120 + 20 * 20) / 100, viewport.Zoom, 10);
        EqualPoint(new SKPoint(140, 110), viewport.WorldToView(world));
    }

    [Theory]
    [InlineData(1e9, SvgEditorViewport.MaximumZoom)]
    [InlineData(1e-9, SvgEditorViewport.MinimumZoom)]
    public void MovingAnchorStaysAttachedAtZoomLimits(double factor, double expectedZoom)
    {
        var viewport = new SvgEditorViewport();
        viewport.Pan(11, -7);
        var previous = new SKPoint(100, 70);
        var next = new SKPoint(125, 85);
        var world = viewport.ViewToWorld(previous);

        viewport.PanAndZoomAt(factor, previous, next);

        Assert.Equal(expectedZoom, viewport.Zoom);
        EqualPoint(next, viewport.WorldToView(world));
        viewport.PanAndZoomAt(factor, next, new SKPoint(135, 80));
        EqualPoint(new SKPoint(135, 80), viewport.WorldToView(world));
    }

    [Fact]
    public void ThirdContactHandoffRebasesWithoutJumping()
    {
        var navigation = new SvgEditorTouchNavigation();
        var viewport = new SvgEditorViewport();
        navigation.AddContact(90, new SKPoint(100, 100));
        navigation.AddContact(20, new SKPoint(200, 100));
        navigation.MoveContact(90, new SKPoint(80, 100), viewport);
        var camera = (viewport.Zoom, viewport.OffsetX, viewport.OffsetY);
        Assert.False(navigation.AddContact(2, new SKPoint(450, 200)));
        Assert.True(navigation.MoveContact(2, new SKPoint(600, 300), viewport));
        Assert.Equal(camera, (viewport.Zoom, viewport.OffsetX, viewport.OffsetY));

        Assert.True(navigation.RemoveContact(90));
        Assert.True(navigation.MoveContact(20, new SKPoint(200, 100), viewport));
        Assert.Equal(camera, (viewport.Zoom, viewport.OffsetX, viewport.OffsetY));
        var world = viewport.ViewToWorld(new SKPoint(400, 200));
        navigation.MoveContact(20, new SKPoint(180, 100), viewport);
        EqualPoint(new SKPoint(390, 200), viewport.WorldToView(world));
    }

    [Fact]
    public void RemainingContactCannotStartDrawingAndReplacementDoesNotJump()
    {
        var navigation = new SvgEditorTouchNavigation();
        var viewport = new SvgEditorViewport();
        navigation.AddContact(1, new SKPoint(10, 20));
        navigation.AddContact(2, new SKPoint(50, 20));
        Assert.True(navigation.RemoveContact(2));
        Assert.True(navigation.IsNavigating);
        Assert.True(navigation.MoveContact(1, new SKPoint(100, 200), viewport));
        Assert.Equal(0, viewport.OffsetX);
        Assert.Equal(0, viewport.OffsetY);
        Assert.False(navigation.AddContact(3, new SKPoint(150, 200)));
        navigation.MoveContact(3, new SKPoint(150, 200), viewport);
        Assert.Equal(1, viewport.Zoom);
        Assert.Equal(0, viewport.OffsetX);
        Assert.True(navigation.RemoveContact(1));
        Assert.True(navigation.RemoveContact(3));
        Assert.False(navigation.IsNavigating);
        Assert.False(navigation.AddContact(4, new SKPoint(20, 30)));
        Assert.False(navigation.MoveContact(4, new SKPoint(30, 40), viewport));
    }

    [Fact]
    public void CancellationClearsAllContactsAndIgnoresLateEvents()
    {
        var navigation = new SvgEditorTouchNavigation();
        var viewport = new SvgEditorViewport();
        navigation.AddContact(1, new SKPoint(10, 20));
        navigation.AddContact(2, new SKPoint(50, 20));
        navigation.Reset();
        Assert.False(navigation.IsNavigating);
        Assert.Equal(0, navigation.ContactCount);
        Assert.False(navigation.Contains(1));
        Assert.False(navigation.MoveContact(1, new SKPoint(200, 300), viewport));
        Assert.False(navigation.RemoveContact(2));
        Assert.False(navigation.AddContact(1, new SKPoint(10, 20)));
        Assert.False(navigation.MoveContact(1, new SKPoint(30, 40), viewport));
        Assert.Equal(1, viewport.Zoom);
        Assert.Equal(0, viewport.OffsetX);
    }

    [Fact]
    public void CoincidentAndInvalidContactsCannotPoisonCamera()
    {
        var navigation = new SvgEditorTouchNavigation();
        var viewport = new SvgEditorViewport();
        Assert.False(navigation.AddContact(5, new SKPoint(float.NaN, 0)));
        Assert.Equal(0, navigation.ContactCount);
        navigation.AddContact(1, default);
        navigation.AddContact(2, default);
        navigation.MoveContact(1, new SKPoint(2, 0), viewport);
        navigation.MoveContact(2, new SKPoint(2, 0), viewport);
        Assert.Equal(1, viewport.Zoom);
        Assert.Equal(2, viewport.OffsetX);
        navigation.MoveContact(1, new SKPoint(float.PositiveInfinity, 0), viewport);
        viewport.PanAndZoomAt(double.NaN, default, default);
        viewport.PanAndZoomAt(-1, default, default);
        viewport.PanAndZoomAt(2, new SKPoint(float.NaN, 0), default);
        Assert.Equal(1, viewport.Zoom);
        Assert.Equal(2, viewport.OffsetX);
        Assert.Equal(0, viewport.OffsetY);
    }

    [Fact]
    public void NavigationLeavesSvgUndoAndRetainedPictureUntouched()
    {
        using var controller = new SvgEditorController();
        controller.LoadSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"100\"><rect id=\"shape\" width=\"40\" height=\"30\" fill=\"red\"/></svg>");
        var source = controller.GetSvg();
        var picture = controller.Renderer.Picture;
        var revision = controller.DocumentRevision;
        var navigation = new SvgEditorTouchNavigation();
        var viewport = new SvgEditorViewport();
        navigation.AddContact(1, new SKPoint(10, 20));
        navigation.AddContact(2, new SKPoint(50, 20));
        for (var i = 0; i < 20; i++)
        {
            navigation.MoveContact(1, new SKPoint(10 - i, 20 + i), viewport);
            navigation.MoveContact(2, new SKPoint(50 + i, 20 + i), viewport);
        }
        navigation.RemoveContact(1);
        navigation.RemoveContact(2);

        Assert.Same(picture, controller.Renderer.Picture);
        Assert.Equal(source, controller.GetSvg());
        Assert.Equal(revision, controller.DocumentRevision);
        Assert.False(controller.IsDirty);
        Assert.False(controller.CanUndo);
    }

    private static void EqualPoint(SKPoint expected, SKPoint actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }
}
