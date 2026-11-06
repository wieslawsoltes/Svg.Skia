using System;
using System.Globalization;
using SkiaSharp;
using Svg.Pathing;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class PathServiceConicSerializationTests
{
    [Theory]
    [InlineData(0.4f)]
    [InlineData(40f)]
    [InlineData(400f)]
    public void CirclesRoundTripThroughSvgWithoutBecomingQuadraticBulges(float radius)
    {
        using var original = new SKPath();
        original.AddCircle(0, 0, radius);
        var points = original.Points;
        using var restored = Reparse(original);
        Assert.Equal(points, original.Points);
        for (var index = 0; index < 360; index++)
        {
            var angle = index * Math.PI / 180;
            var x = (float)Math.Cos(angle);
            var y = (float)Math.Sin(angle);
            Assert.True(restored.Contains(x * radius * 0.99f, y * radius * 0.99f));
            Assert.False(restored.Contains(x * radius * 1.01f, y * radius * 1.01f));
        }
    }

    [Fact]
    public void ArbitraryConicWeightIsNotReplacedWithOne()
    {
        using var original = new SKPath();
        original.MoveTo(0, 0);
        original.ConicTo(50, 100, 100, 0, 0.2f);
        original.Close();
        using var restored = Reparse(original);
        Assert.True(original.Contains(50, 10));
        Assert.True(restored.Contains(50, 10));
        Assert.False(original.Contains(50, 25));
        Assert.False(restored.Contains(50, 25));
    }

    [Fact]
    public void TransformedEllipsesRetainTheirCurvature()
    {
        using var original = new SKPath();
        original.AddOval(SKRect.Create(-60, -15, 120, 30));
        var transform = SKMatrix.CreateRotationDegrees(37);
        original.Transform(transform);
        using var restored = Reparse(original);
        for (var index = 0; index < 360; index++)
        {
            var angle = index * Math.PI / 180;
            var point = new SKPoint(60 * (float)Math.Cos(angle), 15 * (float)Math.Sin(angle));
            var inside = transform.MapPoint(new SKPoint(point.X * 0.99f, point.Y * 0.99f));
            var outside = transform.MapPoint(new SKPoint(point.X * 1.01f, point.Y * 1.01f));
            Assert.True(restored.Contains(inside.X, inside.Y));
            Assert.False(restored.Contains(outside.X, outside.Y));
        }
    }

    [Fact]
    public void NativeConicSerializationIsCultureInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            using var original = new SKPath();
            original.AddCircle(10.5f, 11.25f, 3.75f);
            using var restored = Reparse(original);
            Assert.True(restored.Contains(10.5f, 11.25f));
            Assert.False(restored.Contains(14.5f, 11.25f));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ExistingNonConicLexicalRepresentationIsUnchanged()
    {
        using var path = new SKPath();
        path.MoveTo(1.25f, -2.5f);
        path.QuadTo(3, 4, 5, 6);
        Assert.Equal("M1.25,-2.5 Q3,4 5,6", PathService.ToSvgPathData(path));
        Assert.Throws<ArgumentNullException>(() => PathService.ToSvgPathData(null!));
    }

    [Fact]
    public void ConvertCircleToPathRetainsGeometryAcrossSaveAndReopen()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><circle id='c' cx='50' cy='40' r='30'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgCircle>("c"));
        editor.ConvertSelectionToPaths();
        for (var pass = 0; pass < 3; pass++)
        {
            var path = Assert.IsType<SvgPath>(editor.Document!.GetElementById("c"));
            using var geometry = PathService.ElementToPath(path)!;
            Assert.True(geometry.Contains(70, 60));
            Assert.False(geometry.Contains(72, 62));
            editor.LoadSvg(editor.GetSvg());
        }
    }

    private static SKPath Reparse(SKPath original)
    {
        var data = PathService.ToSvgPathData(original);
        var svg = new SvgPath { PathData = SvgPathBuilder.Parse(data.AsSpan()) };
        var restored = PathService.ElementToPath(svg)!;
        restored.FillType = original.FillType;
        return restored;
    }
}
