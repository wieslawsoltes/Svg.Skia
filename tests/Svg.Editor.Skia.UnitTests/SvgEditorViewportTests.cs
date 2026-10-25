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
}
