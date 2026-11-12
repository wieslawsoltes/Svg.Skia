using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Pathing;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorNodeCommandTests
{
    [Fact]
    public void PathNudgeUsesPictureAxesThroughViewBoxAndAncestorTransforms()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="400" height="300" viewBox="10 20 200 100" preserveAspectRatio="none">
              <g transform="translate(35 7) rotate(25) scale(1.5 .75)">
                <path id="path" transform="skewX(12)" d="m10 20 c5 -10 30 -10 40 0 l20 20" fill="none" stroke="blue"/>
              </g>
            </svg>
            """);
        var path = editor.Document!.GetElementById<SvgPath>("path");
        editor.Select(path);
        var original = editor.GetSvg();
        var before = PathPoints(editor, path);

        Assert.True(editor.NudgeNode(path, 3, new SKPoint(10, -7)));

        Assert.Same(path, Assert.Single(editor.SelectedElements));
        var after = PathPoints(editor, path);
        Assert.Equal(before.Length, after.Length);
        for (var index = 0; index < before.Length; index++)
            Near(index == 3 ? new SKPoint(before[index].X + 10, before[index].Y - 7) : before[index], after[index]);
        Assert.Equal("Nudge path point", Assert.Single(editor.Session.UndoLabels));
        Assert.Equal(1, editor.Session.UndoCount);

        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
        editor.Redo();
        Near(after[3], PathPoints(editor, editor.Document!.GetElementById<SvgPath>("path"))[3]);
    }

    [Fact]
    public void DirectionHandleNudgeChangesOnlyThatHandle()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><path id='path' d='m10 10 c10 0 20 10 30 20 l10 10' fill='none' stroke='black'/></svg>");
        var path = editor.Document!.GetElementById<SvgPath>("path");
        editor.Select(path);
        var original = editor.GetSvg();
        var before = PathPoints(editor, path);

        Assert.True(editor.NudgeNode(path, 1, new SKPoint(-3, 4)));

        var curve = Assert.IsType<SvgCubicCurveSegment>(path.PathData[1]);
        Assert.Equal(17, curve.FirstControlPoint.X, 4);
        Assert.Equal(14, curve.FirstControlPoint.Y, 4);
        var after = PathPoints(editor, path);
        for (var index = 0; index < before.Length; index++)
            Near(index == 1 ? new SKPoint(before[index].X - 3, before[index].Y + 4) : before[index], after[index]);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Theory]
    [InlineData("polygon")]
    [InlineData("polyline")]
    public void VertexNudgeRetainsOtherVerticesAndElementIdentity(string kind)
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg($"""
            <svg xmlns="http://www.w3.org/2000/svg" width="300" height="200" viewBox="0 0 150 100">
              <g transform="translate(20 10) rotate(30) scale(2 .5)">
                <{kind} id="shape" transform="skewY(15)" points="10,10 40,10 40,30 10,30" fill="none" stroke="red" data-label="retained"/>
              </g>
            </svg>
            """);
        var polygon = Assert.IsAssignableFrom<SvgPolygon>(editor.Document!.GetElementById("shape"));
        editor.Select(polygon);
        var original = editor.GetSvg();
        var before = VertexPoints(editor, polygon);

        Assert.True(editor.NudgeNode(polygon, 2, new SKPoint(-4, 10)));

        Assert.Same(polygon, Assert.Single(editor.SelectedElements));
        Assert.Equal("retained", polygon.CustomAttributes["data-label"]);
        Assert.Equal(kind == "polyline", polygon is SvgPolyline);
        var after = VertexPoints(editor, polygon);
        Assert.Equal(4, after.Length);
        for (var index = 0; index < before.Length; index++)
            Near(index == 2 ? new SKPoint(before[index].X - 4, before[index].Y + 10) : before[index], after[index]);
        Assert.Equal("Nudge vertex", Assert.Single(editor.Session.UndoLabels));
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void NoOpAndInvalidIndicesDoNotNormalizeOrCreateHistory()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><path id='path' d='m10 10 l20 0 l0 20' stroke='black'/><rect id='rect' width='20' height='20'/><polygon id='polygon' points='10 10 20 10 20 20'/></svg>");
        var path = editor.Document!.GetElementById<SvgPath>("path");
        var polygon = editor.Document.GetElementById<SvgPolygon>("polygon");
        editor.Select(path);
        var original = editor.GetSvg();

        Assert.False(editor.NudgeNode(path, 1, new SKPoint(0, 0)));
        Assert.False(editor.NudgeNode(path, -1, new SKPoint(1, 0)));
        Assert.False(editor.NudgeNode(path, 100, new SKPoint(1, 0)));
        Assert.False(editor.NudgeNode(polygon, 100, new SKPoint(1, 0)));
        Assert.False(editor.NudgeNode(editor.Document.GetElementById<SvgRectangle>("rect"), 0, new SKPoint(1, 0)));

        Assert.Equal(original, editor.GetSvg());
        Assert.Equal(0, editor.Session.UndoCount);
    }

    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(0f, float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity, 0f)]
    public void InvalidDeltasRejectBeforeMutation(float x, float y)
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><path id='path' d='m10 10 l20 20' stroke='black'/></svg>");
        var original = editor.GetSvg();
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.NudgeNode(editor.Document!.GetElementById<SvgPath>("path"), 0, new SKPoint(x, y)));
        Assert.Equal(original, editor.GetSvg());
        Assert.Equal(0, editor.Session.UndoCount);
    }

    [Theory]
    [InlineData("data-unodraw-locked='true'")]
    [InlineData("transform='scale(0 1)'")]
    public void LockedOrSingularAncestorsRejectBeforeMutation(string attribute)
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg($"<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><g {attribute}><path id='path' d='m10 10 l20 20' fill='none' stroke='black' stroke-width='2'/></g></svg>");
        var original = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.NudgeNode(editor.Document!.GetElementById<SvgPath>("path"), 0, new SKPoint(1, 0)));
        Assert.Equal(original, editor.GetSvg());
        Assert.Equal(0, editor.Session.UndoCount);
    }

    [Fact]
    public void StaleTargetFromBeforeUndoCannotEditTheRestoredDocument()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><path id='path' d='M10 10 L30 30' stroke='black'/></svg>");
        var oldPath = editor.Document!.GetElementById<SvgPath>("path");
        editor.Select(oldPath);
        Assert.True(editor.NudgeNode(oldPath, 0, new SKPoint(1, 0)));
        editor.Undo();
        var restored = editor.GetSvg();

        Assert.Throws<ArgumentException>(() => editor.NudgeNode(oldPath, 0, new SKPoint(1, 0)));

        Assert.Equal(restored, editor.GetSvg());
        Assert.Equal(0, editor.Session.UndoCount);
        Assert.Equal(1, editor.Session.RedoCount);
    }

    private static SKPoint[] PathPoints(SvgEditorController editor, SvgPath path)
    {
        var scene = editor.GetSceneNode(path)!;
        var inspection = new PathService();
        try
        {
            inspection.Start((SvgPath)path.DeepCopy(), scene);
            return inspection.PathPoints.Select(point =>
            {
                var transformed = scene.TotalTransform.MapPoint(point.Point);
                return new SKPoint(transformed.X, transformed.Y);
            }).ToArray();
        }
        finally { inspection.Stop(); }
    }

    private static SKPoint[] VertexPoints(SvgEditorController editor, SvgPolygon polygon)
    {
        var matrix = editor.GetSceneNode(polygon)!.TotalTransform;
        return SvgEditorVertexService.GetVertices(polygon).Select(point =>
        {
            var transformed = matrix.MapPoint(new ShimSkiaSharp.SKPoint(point.X, point.Y));
            return new SKPoint(transformed.X, transformed.Y);
        }).ToArray();
    }

    private static void Near(SKPoint expected, SKPoint actual)
        => Assert.InRange(SKPoint.Distance(expected, actual), 0, 0.005f);
}
