using System;
using System.Collections.Generic;
using System.Drawing;
using ShimSkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Svg.Model;
using Svg.Model.Drawables;
using Svg.Pathing;
using Svg.Transforms;
using Xunit;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.UnitTests;

public class SelectionServiceTests
{
    [Fact]
    public void SnapAndContainsRect_RespectGridAndBounds()
    {
        var service = new SelectionService
        {
            SnapToGrid = true,
            GridSize = 10
        };

        Assert.Equal(20, service.Snap(17));
        Assert.True(SelectionService.ContainsRect(new SK.SKRect(0, 0, 100, 100), new SK.SKRect(10, 10, 90, 90)));
        Assert.False(SelectionService.ContainsRect(new SK.SKRect(0, 0, 10, 10), new SK.SKRect(-1, -1, 5, 5)));
    }

    [Fact]
    public void HitHandle_FindsResizeAndRotationHandles()
    {
        var service = new SelectionService();
        var bounds = new BoundsInfo(
            new SK.SKPoint(0, 0),
            new SK.SKPoint(100, 0),
            new SK.SKPoint(100, 100),
            new SK.SKPoint(0, 100),
            new SK.SKPoint(50, 0),
            new SK.SKPoint(100, 50),
            new SK.SKPoint(50, 100),
            new SK.SKPoint(0, 50),
            new SK.SKPoint(50, 50),
            new SK.SKPoint(50, -20));

        var topLeft = service.HitHandle(bounds, new SK.SKPoint(0, 0), 1f, out var center);
        var rotate = service.HitHandle(bounds, new SK.SKPoint(50, -20), 1f, out _);

        Assert.Equal(new SK.SKPoint(50, 50), center);
        Assert.Equal(0, topLeft);
        Assert.Equal(8, rotate);
    }

    [Fact]
    public void GetInteractiveBounds_UsesDrawableGeometryForStandardElements()
    {
        var ellipse = new SvgEllipse
        {
            CenterX = 50,
            CenterY = 40,
            RadiusX = 20,
            RadiusY = 10
        };

        var drawable = new TestDrawable
        {
            Element = ellipse,
            GeometryBounds = new SKRect(28, 29, 72, 51)
        };

        var bounds = SelectionService.GetInteractiveBounds(drawable);

        Assert.Equal(28f, bounds.Left);
        Assert.Equal(29f, bounds.Top);
        Assert.Equal(72f, bounds.Right);
        Assert.Equal(51f, bounds.Bottom);
    }

    [Fact]
    public void GetLocalCenter_UsesInteractiveBoundsCenter()
    {
        var drawable = new TestDrawable
        {
            Element = new SvgRectangle(),
            GeometryBounds = new SKRect(20, 40, 100, 120)
        };

        var center = SelectionService.GetLocalCenter(drawable);

        Assert.Equal(60f, center.X);
        Assert.Equal(80f, center.Y);
    }

    [Fact]
    public void NormalizeWorldTranslation_MovesSinglePreRotateTranslateToTrailingWorldSpace()
    {
        var service = new SelectionService();
        var rectangle = new SvgRectangle
        {
            Width = 20,
            Height = 20,
            Transforms =
            [
                new SvgTranslate(10, 0),
                new SvgRotate(90, 10, 10)
            ]
        };

        var normalized = service.NormalizeWorldTranslation(rectangle);

        Assert.True(normalized);
        Assert.Collection(
            rectangle.Transforms!,
            transform => Assert.IsType<SvgRotate>(transform),
            transform =>
            {
                var translate = Assert.IsType<SvgTranslate>(transform);
                Assert.True(Math.Abs(translate.X) < 0.001f);
                Assert.True(Math.Abs(Math.Abs(translate.Y) - 10f) < 0.001f);
            });
    }

    [Fact]
    public void ResizeElement_UpdatesPathGeometryForResizeHandles()
    {
        var service = new SelectionService();
        var path = new SvgPath
        {
            PathData =
            [
                new SvgMoveToSegment(false, new PointF(0, 0)),
                new SvgLineSegment(false, new PointF(10, 0)),
                new SvgLineSegment(false, new PointF(10, 10)),
                new SvgLineSegment(false, new PointF(0, 10)),
                new SvgClosePathSegment(false)
            ]
        };

        service.ResizeElement(
            path,
            handle: 4,
            dx: 10,
            dy: 10,
            startRect: new SK.SKRect(0, 0, 10, 10),
            startTransX: 0,
            startTransY: 0,
            startScaleX: 1,
            startScaleY: 1);

        var resizedPath = PathService.ElementToPath(path);
        var bounds = resizedPath!.TightBounds;

        Assert.Equal(0f, bounds.Left);
        Assert.Equal(0f, bounds.Top);
        Assert.Equal(20f, bounds.Right);
        Assert.Equal(20f, bounds.Bottom);
        Assert.Null(path.Transforms);
    }

    private sealed class TestDrawable : DrawableBase
    {
        public TestDrawable() : base(null!, new HashSet<Uri>())
        {
            IsDrawable = true;
            Transform = SKMatrix.Identity;
            TotalTransform = SKMatrix.Identity;
        }

        public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
        {
        }

        public override SKDrawable Clone()
        {
            return new TestDrawable
            {
                Element = Element,
                GeometryBounds = GeometryBounds,
                TransformedBounds = TransformedBounds,
                Transform = Transform,
                TotalTransform = TotalTransform
            };
        }
    }
}
