using System.Drawing;
using System.Linq;
using Svg;
using Svg.Editor.Skia;
using Svg.Pathing;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class PathServiceNodeEditingTests
{
    [Fact]
    public void DeleteInitialAnchorPromotesNextEndpointAndIsUndoable()
    {
        using var editor = Load("M10 10 L20 20 L30 10 Z M50 50 L60 60");
        var source = editor.GetSvg();
        var path = editor.Document!.GetElementById<SvgPath>("path");
        editor.Execute("Delete node", () =>
        {
            editor.Paths.Start(path, editor.GetSceneNode(path)!);
            editor.Paths.ActivePoint = 0;
            editor.Paths.RemoveActivePoint();
            editor.Paths.Stop();
        });
        Assert.Same(path, editor.Document.GetElementById("path"));
        Assert.Equal(new PointF(20, 20), Assert.IsType<SvgMoveToSegment>(path.PathData[0]).End);
        Assert.Equal(5, path.PathData.Count);
        Assert.IsType<SvgClosePathSegment>(path.PathData[2]);
        Assert.IsType<SvgMoveToSegment>(path.PathData[3]);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(source, editor.GetSvg());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void DeleteCubicHandleRetractsItWithoutDeletingAnchors(int handle)
    {
        using var editor = Load("M10 10 C20 0 30 0 40 10 L50 20");
        var path = editor.Document!.GetElementById<SvgPath>("path");
        var service = editor.Paths;
        service.Start(path, editor.GetSceneNode(path)!);
        service.ActivePoint = handle;
        service.RemoveActivePoint();
        var cubic = Assert.IsType<SvgCubicCurveSegment>(path.PathData[1]);
        Assert.Equal(3, path.PathData.Count);
        Assert.Equal(new PointF(40, 10), cubic.End);
        Assert.Equal(handle == 1 ? new PointF(10, 10) : new PointF(20, 0), cubic.FirstControlPoint);
        Assert.Equal(handle == 2 ? cubic.End : new PointF(30, 0), cubic.SecondControlPoint);
        Assert.Equal(handle, service.ActivePoint);
    }

    [Fact]
    public void DeleteQuadraticHandlePreservesBothAnchorsAsALine()
    {
        using var editor = Load("M10 10 Q20 0 30 10");
        var path = editor.Document!.GetElementById<SvgPath>("path");
        editor.Paths.Start(path, editor.GetSceneNode(path)!);
        editor.Paths.ActivePoint = 1;
        editor.Paths.RemoveActivePoint();
        Assert.Equal(new PointF(30, 10), Assert.IsType<SvgLineSegment>(path.PathData[1]).End);
        Assert.Equal(2, path.PathData.Count);
    }

    [Fact]
    public void DeleteOnlyAnchorRemovesItsCloseCommandButPreservesOtherSubpaths()
    {
        using var editor = Load("M10 10 Z M30 30 L40 40");
        var path = editor.Document!.GetElementById<SvgPath>("path");
        editor.Paths.Start(path, editor.GetSceneNode(path)!);
        editor.Paths.ActivePoint = 0;
        editor.Paths.RemoveActivePoint();
        Assert.Equal(2, path.PathData.Count);
        Assert.Equal(new PointF(30, 30), Assert.IsType<SvgMoveToSegment>(path.PathData[0]).End);
        Assert.Same(path, editor.Document.GetElementById("path"));
    }

    [Fact]
    public void NodeEditingResolvesSmoothControlsAndRelativeMoveAfterClose()
    {
        using var editor = Load("M10 10 c10 -10 20 -10 30 0 s20 10 30 0 z m5 5 q10 -10 20 0 t20 0");
        var path = editor.Document!.GetElementById<SvgPath>("path");
        editor.Paths.Start(path, editor.GetSceneNode(path)!);
        Assert.Equal(new PointF(50, 20), Assert.IsType<SvgCubicCurveSegment>(path.PathData[2]).FirstControlPoint);
        Assert.Equal(new PointF(15, 15), Assert.IsType<SvgMoveToSegment>(path.PathData[4]).End);
        Assert.Equal(new PointF(45, 25), Assert.IsType<SvgQuadraticCurveSegment>(path.PathData[6]).ControlPoint);
        Assert.All(editor.Paths.PathPoints, point =>
        {
            Assert.True(float.IsFinite(point.Point.X));
            Assert.True(float.IsFinite(point.Point.Y));
        });
        Assert.All(path.PathData.OfType<SvgCubicCurveSegment>(), segment => Assert.False(segment.IsRelative));
    }

    private static SvgEditorController Load(string data)
    {
        var editor = new SvgEditorController();
        editor.LoadSvg($"<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><path id='path' d='{data}' fill='none' stroke='red'/></svg>");
        return editor;
    }
}
