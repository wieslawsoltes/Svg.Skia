using System;
using SkiaSharp;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorViewportTests
{
    [Fact]
    public void ZoomKeepsDocumentPointUnderCursor()
    {
        var viewport = new SvgEditorViewport();
        viewport.Resize(1200, 800);
        viewport.Pan(120, -64);
        var cursor = new SKPoint(413, 297);
        var world = viewport.ViewToWorld(cursor);

        viewport.ZoomAt(2.25, cursor);

        Assert.Equal(world.X, viewport.ViewToWorld(cursor).X, 4);
        Assert.Equal(world.Y, viewport.ViewToWorld(cursor).Y, 4);
        Assert.Equal(cursor.X, viewport.WorldToView(world).X, 3);
        Assert.Equal(cursor.Y, viewport.WorldToView(world).Y, 3);
    }

    [Fact]
    public void RepeatedReciprocalZoomsDoNotDrift()
    {
        var viewport = new SvgEditorViewport();
        viewport.Resize(1200, 800);
        viewport.Pan(123.45, -67.89);
        var cursor = new SKPoint(355.5f, 207.25f);
        for (var i = 0; i < 1000; i++)
        {
            viewport.ZoomAt(1.2, cursor);
            viewport.ZoomAt(1 / 1.2, cursor);
        }

        Assert.Equal(1, viewport.Zoom, 10);
        Assert.Equal(123.45, viewport.OffsetX, 8);
        Assert.Equal(-67.89, viewport.OffsetY, 8);
    }

    [Fact]
    public void FitCentersNonzeroOriginDocumentWithPadding()
    {
        var viewport = new SvgEditorViewport();
        viewport.Resize(1000, 600);
        viewport.Fit(new SKRect(-200, 100, 600, 500), 100);

        Assert.Equal(1, viewport.Zoom);
        Assert.Equal(new SKPoint(500, 300), viewport.WorldToView(new SKPoint(200, 300)));
        Assert.Equal(new SKPoint(100, 100), viewport.WorldToView(new SKPoint(-200, 100)));
        Assert.Equal(new SKPoint(900, 500), viewport.WorldToView(new SKPoint(600, 500)));
    }

    [Fact]
    public void ResizePreservesWorldCenter()
    {
        var viewport = new SvgEditorViewport();
        viewport.Resize(800, 600);
        viewport.Pan(80, 45);
        viewport.ZoomAt(2, new SKPoint(300, 200));
        var center = viewport.ViewToWorld(new SKPoint(400, 300));

        viewport.Resize(1400, 900);

        Assert.Equal(center, viewport.ViewToWorld(new SKPoint(700, 450)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 450)]
    [InlineData(900, 0)]
    public void ZeroSizedLayoutAndRestorePreserveTheCamera(double width, double height)
    {
        var viewport = new SvgEditorViewport();
        viewport.Resize(1000, 800);
        viewport.Pan(125.375, -98.25);
        viewport.ZoomAt(0.7516666666666667, new SKPoint(413.5f, 207.25f));
        var offsetX = viewport.OffsetX;
        var offsetY = viewport.OffsetY;
        var zoom = viewport.Zoom;
        var center = WorldCenter(viewport);

        viewport.Resize(width, height);

        Assert.Equal(width, viewport.Width);
        Assert.Equal(height, viewport.Height);
        AssertWorldCenter(center, viewport);
        viewport.Resize(1000, 800);

        AssertWorldCenter(center, viewport);
        Assert.Equal(offsetX, viewport.OffsetX, 10);
        Assert.Equal(offsetY, viewport.OffsetY, 10);
        Assert.Equal(zoom, viewport.Zoom);
    }

    [Fact]
    public void RepeatedZeroAxisLayoutsPreserveTheCurrentPannedAndZoomedCenter()
    {
        var viewport = new SvgEditorViewport();
        viewport.Resize(1000, 800);
        viewport.Fit(new SKRect(0, 0, 1200, 800));
        var center = WorldCenter(viewport);
        var sizes = new (double Width, double Height)[]
        {
            (0, 300), (200, 0), (0, 0), (0, 450), (390, 0), (390, 692), (1000, 800)
        };
        for (var cycle = 0; cycle < 100; cycle++)
        {
            viewport.Resize(0, 0);
            viewport.Pan(17.25, -23.5);
            center = (center.X - 17.25 / viewport.Zoom, center.Y + 23.5 / viewport.Zoom);
            // The zero-sized viewport's center is the zoom anchor. Restoration
            // must keep this edited camera, not a snapshot from before collapse.
            viewport.ZoomAt(cycle % 2 == 0 ? 1.25 : 0.8, default);
            foreach (var size in sizes)
            {
                viewport.Resize(size.Width, size.Height);
                AssertWorldCenter(center, viewport);
            }
        }
    }

    [Fact]
    public void FirstPositiveSizeDoesNotRecenterAnUninitializedViewport()
    {
        var viewport = new SvgEditorViewport();
        viewport.Pan(42.5, -16.25);
        viewport.ZoomAt(2, new SKPoint(75, 30));
        var offsetX = viewport.OffsetX;
        var offsetY = viewport.OffsetY;

        viewport.Resize(0, 50);
        viewport.Resize(80, 0);
        viewport.Resize(0, 0);
        viewport.Resize(800, 600);

        Assert.Equal(offsetX, viewport.OffsetX);
        Assert.Equal(offsetY, viewport.OffsetY);
        var center = WorldCenter(viewport);
        viewport.Resize(1000, 900);
        AssertWorldCenter(center, viewport);
    }

    [Fact]
    public void RejectedFirstSizeDoesNotInitializeCenterPreservation()
    {
        var viewport = new SvgEditorViewport();
        viewport.SetZoomAt(SvgEditorViewport.MinimumZoom, default);
        var changes = 0;
        viewport.Changed += (_, _) => changes++;

        viewport.Resize(double.NaN, 600);
        viewport.Resize(800, double.PositiveInfinity);
        viewport.Resize(-1, 600);
        // Each dimension is finite, but its world extent exceeds float range.
        viewport.Resize(float.MaxValue, 600);

        Assert.Equal(0, changes);
        Assert.Equal(0, viewport.Width);
        Assert.Equal(0, viewport.Height);
        viewport.Resize(800, 600);
        Assert.Equal(1, changes);
        Assert.Equal(0, viewport.OffsetX);
        Assert.Equal(0, viewport.OffsetY);
        Assert.Equal(SvgEditorViewport.MinimumZoom, viewport.Zoom);
    }

    [Fact]
    public void InvalidCameraInputCannotPoisonTransform()
    {
        var viewport = new SvgEditorViewport();
        viewport.Resize(800, 600);
        viewport.ZoomAt(double.NaN, default);
        viewport.ZoomAt(double.PositiveInfinity, default);
        viewport.SetZoomAt(-1, default);
        viewport.SetZoomAt(2, new SKPoint(float.NaN, 0));
        viewport.Pan(double.PositiveInfinity, 0);
        viewport.Pan(double.MaxValue, 0);
        viewport.Resize(double.MaxValue, 600);
        viewport.Fit(new SKRect(float.NaN, 0, 10, 10));
        viewport.Fit(new SKRect(0, 0, 10, 0));
        viewport.Fit(new SKRect(-float.MaxValue, 0, float.MaxValue, 100));

        Assert.Equal(1, viewport.Zoom);
        Assert.Equal(0, viewport.OffsetX);
        Assert.Equal(0, viewport.OffsetY);
        Assert.Equal(800, viewport.Width);
        Assert.Equal(600, viewport.Height);
    }

    [Fact]
    public void ZoomClampsAndRetainsAnchorAtBothLimits()
    {
        var viewport = new SvgEditorViewport();
        var cursor = new SKPoint(50, 30);
        var world = viewport.ViewToWorld(cursor);

        viewport.ZoomAt(1e9, cursor);
        Assert.Equal(SvgEditorViewport.MaximumZoom, viewport.Zoom);
        Assert.Equal(world, viewport.ViewToWorld(cursor));

        viewport.ZoomAt(1e-12, cursor);
        Assert.Equal(SvgEditorViewport.MinimumZoom, viewport.Zoom);
        Assert.Equal(world, viewport.ViewToWorld(cursor));
    }

    private static (double X, double Y) WorldCenter(SvgEditorViewport viewport)
        => ((viewport.Width / 2 - viewport.OffsetX) / viewport.Zoom,
            (viewport.Height / 2 - viewport.OffsetY) / viewport.Zoom);

    private static void AssertWorldCenter((double X, double Y) expected, SvgEditorViewport viewport)
    {
        var actual = WorldCenter(viewport);
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
    }
}
