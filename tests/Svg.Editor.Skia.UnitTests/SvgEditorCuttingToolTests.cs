using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Pathing;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorCuttingToolTests
{
    [Fact]
    public void ScissorsSplitsTransformedCubicWithoutFlatteningAndUndoesAtomically()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='240' height='100'><g id='layer' transform='translate(20 10) scale(2 1)'><path id='curve' d='M10 50 C20 0 80 0 90 50' fill='none' stroke='blue' stroke-width='4'/></g></svg>");
        var originalPath = editor.Document!.GetElementById<SvgPath>("curve");
        editor.Select(originalPath);
        var original = editor.GetSvg();
        var parent = originalPath.Parent;
        var originalStroke = originalPath.Stroke.ToString();
        Assert.True(editor.CutPathAt(new SKPoint(120, 22.5f), 3));
        var pieces = editor.SelectedElements.OfType<SvgPath>().ToArray();
        Assert.Equal(2, pieces.Length);
        Assert.Equal(2, pieces.Select(path => path.ID).Distinct().Count());
        Assert.Equal("curve", pieces[0].ID);
        Assert.All(pieces, path =>
        {
            Assert.Same(parent, path.Parent);
            Assert.Contains(path.PathData, segment => segment is SvgCubicCurveSegment);
            Assert.Equal(originalStroke, path.Stroke.ToString());
            Assert.Equal(4, path.StrokeWidth.Value);
        });
        using var before = WorldPath(editor, pieces[0]);
        using var after = WorldPath(editor, pieces[1]);
        using var firstMeasure = new SKPathMeasure(before, false, 4);
        using var secondMeasure = new SKPathMeasure(after, false, 4);
        Assert.True(firstMeasure.GetPositionAndTangent(firstMeasure.Length, out var end, out _));
        Assert.True(secondMeasure.GetPositionAndTangent(0, out var start, out _));
        Assert.InRange(SKPoint.Distance(end, new SKPoint(120, 22.5f)), 0, 0.05f);
        Assert.InRange(SKPoint.Distance(start, end), 0, 0.001f);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
        editor.Redo();
        Assert.Equal(2, editor.Document!.Descendants().OfType<SvgPath>().Count());
    }

    [Fact]
    public void ScissorsFirstOpensClosedContourAndSecondCutSeparatesIt()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='120' height='100'><rect id='box' x='10' y='10' width='80' height='60' fill='none' stroke='black'/></svg>");
        Assert.True(editor.CutPathAt(new SKPoint(50, 10), 3));
        var opened = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.DoesNotContain(opened.PathData, segment => segment is SvgClosePathSegment);
        Assert.True(editor.CutPathAt(new SKPoint(50, 70), 3));
        Assert.Equal(2, editor.SelectedElements.Count);
        var length = 0f;
        foreach (var piece in editor.SelectedElements)
        {
            using var path = WorldPath(editor, piece);
            using var measure = new SKPathMeasure(path, false, 4);
            Assert.False(measure.IsClosed);
            length += measure.Length;
        }
        Assert.Equal(280, length, 3);
        Assert.Equal(2, editor.Session.UndoCount);
    }

    [Fact]
    public void ScissorsRetainsOtherCompoundContoursAndMissesDoNotCreateHistory()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><path id='compound' d='M10 10 L90 10 M10 50 L90 50' stroke='black' fill='none'/></svg>");
        var original = editor.GetSvg();
        Assert.False(editor.CutPathAt(new SKPoint(10, 10), 2));
        Assert.False(editor.CutPathAt(new SKPoint(150, 80), 2));
        Assert.False(editor.CanUndo);
        Assert.Equal(original, editor.GetSvg());
        Assert.True(editor.CutPathAt(new SKPoint(50, 10), 2));
        Assert.Equal(2, editor.SelectedElements.Count);
        Assert.Equal(3, editor.SelectedElements.OfType<SvgPath>().Sum(path => path.PathData.OfType<SvgMoveToSegment>().Count()));
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void EraserUsesPictureWidthInsideImportedAndNestedTransforms()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='300' height='150' viewBox='0 0 150 75'><g id='layer' transform='translate(10 5) scale(2 1)' fill='red'><rect id='box' width='40' height='30'/></g></svg>");
        var original = editor.GetSvg();
        var parent = editor.Document!.GetElementById<SvgGroup>("layer");
        Assert.True(editor.EraseAlong(new[] { new SKPoint(100, 0), new SKPoint(100, 80) }, 20));
        var result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Same(parent, result.Parent);
        Assert.Equal("box", result.ID);
        using var geometry = WorldPath(editor, result);
        Assert.True(geometry.Contains(50, 40));
        Assert.False(geometry.Contains(100, 40));
        Assert.True(geometry.Contains(150, 40));
        Assert.True(geometry.Contains(89, 40));
        Assert.False(geometry.Contains(91, 40));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void EraserOutlinesDashedOpenStrokesAndKeepsTheRemainingDashPattern()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='80'><path id='line' d='M10 40 H190' fill='none' stroke='blue' stroke-width='8' stroke-dasharray='12 8'/></svg>");
        var original = editor.GetSvg();
        var stroke = editor.Document!.GetElementById<SvgPath>("line").Stroke.ToString();
        Assert.True(editor.EraseAlong(new[] { new SKPoint(100, 10), new SKPoint(100, 70) }, 20));
        var result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Equal(SvgPaintServer.None, result.Stroke);
        Assert.Equal(stroke, result.Fill.ToString());
        using var geometry = WorldPath(editor, result);
        Assert.True(geometry.Contains(16, 40));
        Assert.False(geometry.Contains(26, 40));
        Assert.True(geometry.Contains(34, 40));
        Assert.False(geometry.Contains(100, 40));
        Assert.True(geometry.Contains(116, 40));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void EraserClickCanDeleteEntireObjectAndSelectionRestrictsTheTargets()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect id='back' x='20' y='20' width='20' height='20' fill='red'/><rect id='front' x='20' y='20' width='20' height='20' fill='blue'/></svg>");
        var front = editor.Document!.GetElementById<SvgRectangle>("front");
        editor.Select(front);
        var original = editor.GetSvg();
        Assert.True(editor.EraseAlong(new[] { new SKPoint(30, 30) }, 40));
        Assert.Null(editor.Document.GetElementById("front"));
        Assert.NotNull(editor.Document.GetElementById("back"));
        Assert.Empty(editor.SelectedElements);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void KnifePreservesFilledAreaAndHolesUnderNestedTransforms()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='220' height='140'><g id='layer' transform='translate(20 10) scale(1.5 1)'><path id='ring' fill='green' fill-rule='evenodd' d='M0 0 H100 V100 H0 Z M30 30 H70 V70 H30 Z'/></g></svg>");
        var source = editor.Document!.GetElementById<SvgPath>("ring");
        editor.Select(source);
        var original = editor.GetSvg();
        var parent = source.Parent;
        using var originalGeometry = WorldPath(editor, source);
        Assert.True(editor.KnifeAlong(new[] { new SKPoint(95, 0), new SKPoint(95, 120) }));
        var pieces = editor.SelectedElements.OfType<SvgPath>().ToArray();
        Assert.Equal(2, pieces.Length);
        Assert.All(pieces, piece => Assert.Same(parent, piece.Parent));
        using var first = WorldPath(editor, pieces[0]);
        using var second = WorldPath(editor, pieces[1]);
        using var union = first.Op(second, SKPathOp.Union);
        Assert.NotNull(union);
        using var lost = originalGeometry.Op(union!, SKPathOp.Xor);
        Assert.NotNull(lost);
        Assert.True(lost!.IsEmpty);
        using var overlap = first.Op(second, SKPathOp.Intersect);
        Assert.NotNull(overlap);
        Assert.True(overlap!.IsEmpty);
        Assert.False(union!.Contains(95, 60));
        Assert.True(union.Contains(40, 60));
        Assert.True(union.Contains(150, 60));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void FreehandKnifeCutsMultipleSelectedShapesInOneUndoWithoutTouchingDistantArtwork()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='300' height='120'><g id='layer' fill='orange'><rect id='a' x='10' y='10' width='80' height='80'/><rect id='b' x='110' y='10' width='80' height='80'/><rect id='distant' x='220' y='10' width='30' height='80'/></g></svg>");
        editor.Select(editor.Document!.GetElementById<SvgGroup>("layer"));
        var original = editor.GetSvg();
        Assert.True(editor.KnifeAlong(new[] { new SKPoint(0, 50), new SKPoint(40, 40), new SKPoint(80, 60), new SKPoint(140, 40), new SKPoint(200, 50) }));
        Assert.Equal(4, editor.SelectedElements.Count);
        Assert.IsType<SvgRectangle>(editor.Document.GetElementById("distant"));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void CuttingProtectsLocksAndInvalidInputNeverCreatesHistory()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect id='box' x='10' y='10' width='80' height='80'/></svg>");
        var original = editor.GetSvg();
        Assert.False(editor.EraseAlong(new[] { new SKPoint(200, 200) }, 10));
        Assert.False(editor.KnifeAlong(new[] { new SKPoint(200, 0), new SKPoint(200, 100) }));
        Assert.Throws<ArgumentException>(() => editor.EraseAlong(new[] { new SKPoint(float.NaN, 20) }, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.EraseAlong(new[] { new SKPoint(20, 20) }, -1));
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
        var target = editor.Document!.GetElementById<SvgRectangle>("box");
        editor.Select(target);
        editor.SetLocked(true);
        editor.Select(target, allowLocked: true);
        var locked = editor.GetSvg();
        var count = editor.Session.UndoCount;
        Assert.Throws<InvalidOperationException>(() => editor.CutPathAt(new SKPoint(50, 10)));
        Assert.Throws<InvalidOperationException>(() => editor.EraseAlong(new[] { new SKPoint(50, 50) }, 10));
        Assert.Throws<InvalidOperationException>(() => editor.KnifeAlong(new[] { new SKPoint(50, 0), new SKPoint(50, 100) }));
        Assert.Equal(locked, editor.GetSvg());
        Assert.Equal(count, editor.Session.UndoCount);
        editor.Select(null);
        Assert.False(editor.EraseAlong(new[] { new SKPoint(50, 50) }, 10));
        Assert.Equal(locked, editor.GetSvg());
    }

    [Fact]
    public void PreparedCutGeometryDoesNotMutateInputAndRoundSweepIncludesTheEndCaps()
    {
        using var path = SKPath.ParseSvgPathData("M0 0 C0 100 100 100 100 0");
        var before = PathService.ToSvgPathData(path);
        var location = SvgPathCuttingService.FindNearest(path, new SKPoint(50, 75), 2);
        Assert.NotNull(location);
        var pieces = SvgPathCuttingService.SplitAt(path, location!.Value);
        try
        {
            Assert.Equal(2, pieces.Count);
            Assert.Equal(before, PathService.ToSvgPathData(path));
        }
        finally { foreach (var piece in pieces) piece.Dispose(); }
        using var sweep = SvgPathCuttingService.CreateSweep(new[] { new SKPoint(10, 10), new SKPoint(50, 10) }, 20);
        Assert.True(sweep.Contains(1, 10));
        Assert.True(sweep.Contains(59, 10));
        Assert.False(sweep.Contains(61, 10));
    }

    private static SKPath WorldPath(SvgEditorController editor, SvgVisualElement element)
    {
        var path = PathService.ElementToPath(element)!;
        var scene = editor.GetSceneNode(element);
        Assert.NotNull(scene);
        var matrix = scene!.TotalTransform;
        path.Transform(new SKMatrix(matrix.ScaleX, matrix.SkewX, matrix.TransX, matrix.SkewY, matrix.ScaleY, matrix.TransY, 0, 0, 1));
        return path;
    }
}
