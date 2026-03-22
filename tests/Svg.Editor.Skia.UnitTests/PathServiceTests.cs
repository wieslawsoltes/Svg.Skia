using System;
using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Model;
using Svg.Model.Drawables;
using Svg.Pathing;
using Svg.Transforms;
using Xunit;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia.UnitTests;

public class PathServiceTests
{
    [Fact]
    public void MakeSmooth_CreatesMoveAndLineSegments()
    {
        var segments = PathService.MakeSmooth(new List<Shim.SKPoint>
        {
            new(0, 0),
            new(5, 5),
            new(10, 0)
        });

        Assert.NotNull(segments);
        Assert.IsType<SvgMoveToSegment>(segments[0]);
        Assert.True(segments.Count >= 2);
    }

    [Fact]
    public void ElementToPath_ConvertsRectangleGeometry()
    {
        var rectangle = new SvgRectangle
        {
            X = 1,
            Y = 2,
            Width = 10,
            Height = 20
        };

        var path = PathService.ElementToPath(rectangle);

        Assert.NotNull(path);
        var svgPath = PathService.ToSvgPathData(path!);
        Assert.Contains("M", svgPath);
        Assert.Contains("L", svgPath);
    }

    [Fact]
    public void NormalizeEditablePath_BakesAxisAlignedTransformsIntoPathData()
    {
        var path = new SvgPath
        {
            PathData = new SvgPathSegmentList
            {
                new SvgMoveToSegment(false, new System.Drawing.PointF(0f, 0f)),
                new SvgLineSegment(false, new System.Drawing.PointF(10f, 0f))
            },
            Transforms = new SvgTransformCollection
            {
                new SvgTranslate(25f, 30f)
            }
        };

        var changed = PathService.NormalizeEditablePath(path);

        Assert.True(changed);
        Assert.NotNull(path.Transforms);
        Assert.Empty(path.Transforms);
        var move = Assert.IsType<SvgMoveToSegment>(path.PathData[0]);
        var line = Assert.IsType<SvgLineSegment>(path.PathData[1]);
        Assert.Equal(25f, move.End.X);
        Assert.Equal(30f, move.End.Y);
        Assert.Equal(35f, line.End.X);
        Assert.Equal(30f, line.End.Y);
    }

    [Fact]
    public void MoveActivePoint_MovesAdjacentControlPointsWithAnchor()
    {
        var path = new SvgPath
        {
            PathData = new SvgPathSegmentList
            {
                new SvgMoveToSegment(false, new System.Drawing.PointF(0f, 0f)),
                new SvgCubicCurveSegment(
                    false,
                    new System.Drawing.PointF(10f, 0f),
                    new System.Drawing.PointF(20f, 0f),
                    new System.Drawing.PointF(30f, 0f)),
                new SvgCubicCurveSegment(
                    false,
                    new System.Drawing.PointF(40f, 0f),
                    new System.Drawing.PointF(50f, 0f),
                    new System.Drawing.PointF(60f, 0f))
            }
        };

        var service = new PathService();
        var drawable = new TestDrawable(new TestAssetLoader(), null)
        {
            TotalTransform = SKMatrix.CreateIdentity()
        };

        service.Start(path, drawable);
        service.ActivePoint = 3;
        service.MoveActivePoint(new Shim.SKPoint(35f, 10f));

        var first = Assert.IsType<SvgCubicCurveSegment>(path.PathData[1]);
        var second = Assert.IsType<SvgCubicCurveSegment>(path.PathData[2]);
        Assert.Equal(35f, first.End.X);
        Assert.Equal(10f, first.End.Y);
        Assert.Equal(25f, first.SecondControlPoint.X);
        Assert.Equal(10f, first.SecondControlPoint.Y);
        Assert.Equal(45f, second.FirstControlPoint.X);
        Assert.Equal(10f, second.FirstControlPoint.Y);
    }

    private sealed class TestAssetLoader : ISvgAssetLoader
    {
        public SKImage LoadImage(System.IO.Stream stream)
            => new() { Data = SKImage.FromStream(stream) };

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
            => new();

        public SKFontMetrics GetFontMetrics(SKPaint paint)
            => default;

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
            => 0f;

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y)
            => new SKPath();
    }

    private sealed class TestDrawable : DrawableBase
    {
        public TestDrawable(ISvgAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
        {
        }

        public override SKDrawable Clone()
        {
            var clone = new TestDrawable(AssetLoader, CloneReferences(References));
            CopyTo(clone, Parent);
            return clone;
        }
    }
}
