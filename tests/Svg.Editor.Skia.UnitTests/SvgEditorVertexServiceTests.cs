using System;
using System.Drawing;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Transforms;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorVertexServiceTests
{
    [Fact]
    public void MoveVertex_PreservesElementIdentityPresentationMetadataAndUnitTypes()
    {
        var polygon = new SvgPolygon
        {
            ID = "editable-polygon",
            Points = new SvgPointCollection
            {
                new(SvgUnitType.User, 0), new(SvgUnitType.User, 0),
                new(SvgUnitType.Pixel, 10), new(SvgUnitType.Point, 5),
                new(SvgUnitType.User, 20), new(SvgUnitType.User, 20)
            },
            Fill = new SvgColourServer(Color.Coral),
            Opacity = 0.4f,
            Transforms = new SvgTransformCollection { new SvgTranslate(7, 11) }
        };
        polygon.CustomAttributes["data-note"] = "keep vertex metadata";
        polygon.Children.Add(new SvgTitle { Content = "Editable polygon" });
        var originalPoints = polygon.Points;
        var fill = polygon.Fill;
        var transforms = polygon.Transforms;

        Assert.True(SvgEditorVertexService.MoveVertex(polygon, 1, new SKPoint(30, 40)));

        Assert.Equal("editable-polygon", polygon.ID);
        Assert.Same(fill, polygon.Fill);
        Assert.Same(transforms, polygon.Transforms);
        Assert.Equal(0.4f, polygon.Opacity);
        Assert.Equal("keep vertex metadata", polygon.CustomAttributes["data-note"]);
        Assert.Equal("Editable polygon", Assert.IsType<SvgTitle>(Assert.Single(polygon.Children)).Content);
        Assert.NotSame(originalPoints, polygon.Points);
        Assert.Equal(10, originalPoints[2].Value);
        Assert.Equal(5, originalPoints[3].Value);
        Assert.Equal(SvgUnitType.Pixel, polygon.Points[2].Type);
        Assert.Equal(SvgUnitType.Point, polygon.Points[3].Type);
        Assert.Equal(new[] { new SKPoint(0, 0), new SKPoint(30, 40), new SKPoint(20, 20) }, SvgEditorVertexService.GetVertices(polygon));
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    public void RemoveVertex_PreservesMinimumRenderableGeometry(bool isPolyline, int minimum)
    {
        var polygon = CreatePolygon(isPolyline);
        if (isPolyline)
            Assert.True(SvgEditorVertexService.RemoveVertex(polygon, 3));

        Assert.True(SvgEditorVertexService.RemoveVertex(polygon, 1));
        var finalPoints = polygon.Points;

        Assert.Equal(minimum, SvgEditorVertexService.GetVertices(polygon).Count);
        Assert.False(SvgEditorVertexService.RemoveVertex(polygon, 0));
        Assert.Same(finalPoints, polygon.Points);
        Assert.Equal("shape", polygon.ID);
    }

    [Fact]
    public void InsertVertex_PreservesOpenPolylineAndAppendsInOrder()
    {
        var polyline = new SvgPolyline
        {
            Points = new SvgPointCollection { 0, 0, 10, 10 },
            ID = "open-line",
            Stroke = new SvgColourServer(Color.Black)
        };

        Assert.True(SvgEditorVertexService.InsertVertex(polyline, 1, new SKPoint(4, 6)));
        Assert.True(SvgEditorVertexService.InsertVertex(polyline, 3, new SKPoint(20, 12)));

        Assert.Equal(new[] { new SKPoint(0, 0), new SKPoint(4, 6), new SKPoint(10, 10), new SKPoint(20, 12) }, SvgEditorVertexService.GetVertices(polyline));
        Assert.All(polyline.Points, unit => Assert.Equal(SvgUnitType.User, unit.Type));
        Assert.Equal("open-line", polyline.ID);
        Assert.IsType<SvgPolyline>(polyline);
    }

    [Fact]
    public void InvalidIndicesAndOddPointLists_LeaveOriginalCollectionUntouched()
    {
        var polygon = CreatePolygon(false);
        var points = polygon.Points;

        Assert.False(SvgEditorVertexService.MoveVertex(polygon, -1, new SKPoint(1, 2)));
        Assert.False(SvgEditorVertexService.InsertVertex(polygon, 5, new SKPoint(1, 2)));
        Assert.False(SvgEditorVertexService.RemoveVertex(polygon, 4));
        Assert.Same(points, polygon.Points);

        polygon.Points.Add(99);
        Assert.False(SvgEditorVertexService.MoveVertex(polygon, 0, new SKPoint(1, 2)));
        Assert.False(SvgEditorVertexService.InsertVertex(polygon, 0, new SKPoint(1, 2)));
        Assert.False(SvgEditorVertexService.RemoveVertex(polygon, 0));
        Assert.Same(points, polygon.Points);
        Assert.Equal(9, polygon.Points.Count);
    }

    [Theory]
    [InlineData(float.NaN, 0)]
    [InlineData(0, float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity, 0)]
    public void NonfiniteVertex_DoesNotCorruptGeometry(float x, float y)
    {
        var polygon = CreatePolygon(false);
        var points = polygon.Points;

        Assert.Throws<ArgumentOutOfRangeException>(() => SvgEditorVertexService.MoveVertex(polygon, 0, new SKPoint(x, y)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgEditorVertexService.InsertVertex(polygon, 0, new SKPoint(x, y)));
        Assert.Same(points, polygon.Points);
    }

    [Fact]
    public void VertexEdit_InTransformedGroupRoundTripsUndoWithoutConvertingThePolygon()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="300" height="200">
              <g id="parent" transform="translate(40 20) scale(2 3)">
                <polygon id="shape" points="0,0 10,0 10,10 0,10" fill="coral" data-note="native polygon" />
              </g>
            </svg>
            """);
        var polygon = editor.Document!.GetElementById<SvgPolygon>("shape");
        editor.Select(polygon);
        var originalXml = editor.GetSvg();
        var before = editor.GetSceneNode(polygon)!.TransformedBounds;

        editor.Execute("Move polygon vertex", () =>
            Assert.True(SvgEditorVertexService.MoveVertex(polygon, 1, new SKPoint(20, 0))));

        var after = editor.GetSceneNode(polygon)!.TransformedBounds;
        Assert.InRange(after.Width - before.Width, 19.99f, 20.01f);
        Assert.Equal(before.Left, after.Left);
        Assert.Equal(before.Top, after.Top);
        Assert.Equal(before.Height, after.Height);
        Assert.Equal(1, editor.Session.UndoCount);
        Assert.True(editor.IsDirty);
        var editedXml = editor.GetSvg();

        editor.Undo();

        Assert.Equal(originalXml, editor.GetSvg());
        Assert.False(editor.IsDirty);
        Assert.IsType<SvgPolygon>(Assert.Single(editor.SelectedElements));
        editor.Redo();
        Assert.Equal(editedXml, editor.GetSvg());
        var restored = Assert.IsType<SvgPolygon>(Assert.Single(editor.SelectedElements));
        Assert.Equal("native polygon", restored.CustomAttributes["data-note"]);
        Assert.Equal(new SKPoint(20, 0), SvgEditorVertexService.GetVertices(restored)[1]);
    }

    private static SvgPolygon CreatePolygon(bool isPolyline)
    {
        SvgPolygon polygon = isPolyline ? new SvgPolyline() : new SvgPolygon();
        polygon.ID = "shape";
        polygon.Points = new SvgPointCollection { 0, 0, 10, 0, 10, 10, 0, 10 };
        return polygon;
    }
}
