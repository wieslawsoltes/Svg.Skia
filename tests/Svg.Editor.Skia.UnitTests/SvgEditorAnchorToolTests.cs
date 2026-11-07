using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Pathing;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorAnchorToolTests
{
    [Fact]
    public void AddAnchorSubdividesTransformedCubicWithoutChangingItsGeometry()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='240' height='120'><g transform='translate(20 10) scale(2 1)'><path id='curve' d='M10 50 C20 0 80 0 90 50' fill='none' stroke='blue'/></g></svg>");
        var source = editor.Document!.GetElementById<SvgPath>("curve");
        editor.Select(source);
        var original = editor.GetSvg();
        using var before = PathService.ElementToPath(source)!;
        Assert.True(editor.AddAnchorAt(new SKPoint(120, 22.5f), 2));
        var result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Equal(2, result.PathData.OfType<SvgCubicCurveSegment>().Count());
        using var after = PathService.ElementToPath(result)!;
        using var beforeMeasure = new SKPathMeasure(before, false, 8);
        using var afterMeasure = new SKPathMeasure(after, false, 8);
        Assert.InRange(Math.Abs(beforeMeasure.Length - afterMeasure.Length), 0, 0.05f);
        for (var index = 0; index <= 20; index++)
        {
            Assert.True(beforeMeasure.GetPositionAndTangent(beforeMeasure.Length * index / 20, out var a, out _));
            Assert.True(afterMeasure.GetPositionAndTangent(afterMeasure.Length * index / 20, out var b, out _));
            Assert.InRange(SKPoint.Distance(a, b), 0, 0.1f);
        }
        Assert.Equal(1, editor.Session.UndoCount);
        Assert.False(editor.AddAnchorAt(new SKPoint(120, 22.5f), 2));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void DeleteAnchorPromotesSubpathStartWithoutInvalidClosingSegments()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><path id='path' d='M10 10 L90 10 L90 90 Z M20 20 L30 30' stroke='black' fill='none'/></svg>");
        var original = editor.GetSvg();
        Assert.True(editor.DeleteAnchorAt(new SKPoint(10, 10), 2));
        var result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.IsType<SvgMoveToSegment>(result.PathData[0]);
        Assert.Equal(90, result.PathData[0].End.X);
        Assert.Equal(2, result.PathData.OfType<SvgMoveToSegment>().Count());
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void AnchorConversionPreviewIsTransientAndUsesPictureSpaceSymmetricHandles()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='240' height='140'><g transform='translate(20 10) scale(2 1)'><path id='path' d='M10 40 L50 20 L90 40' fill='none' stroke='blue'/></g></svg>");
        var original = editor.GetSvg();
        using (var cancelled = editor.PrepareAnchorConversion(new SKPoint(120, 30), new SKPoint(20, 0), 2))
        {
            Assert.NotNull(cancelled);
            Assert.Equal(original, editor.GetSvg());
            Assert.False(editor.CanUndo);
        }
        using var preview = editor.PrepareAnchorConversion(new SKPoint(120, 30), new SKPoint(20, 0), 2);
        Assert.NotNull(preview);
        editor.ApplyAnchorConversion(preview!);
        var result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        var incoming = Assert.IsType<SvgCubicCurveSegment>(result.PathData[1]);
        var outgoing = Assert.IsType<SvgCubicCurveSegment>(result.PathData[2]);
        Assert.Equal(40, incoming.SecondControlPoint.X, 4);
        Assert.Equal(20, incoming.SecondControlPoint.Y, 4);
        Assert.Equal(60, outgoing.FirstControlPoint.X, 4);
        Assert.Equal(20, outgoing.FirstControlPoint.Y, 4);
        Assert.Equal(1, editor.Session.UndoCount);
        Assert.Throws<InvalidOperationException>(() => editor.ApplyAnchorConversion(preview!));
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void AnchorConversionAtClosedStartUpdatesBothSidesAndCornerClickRetractsHandles()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='120' height='120'><path id='path' d='M10 10 L90 10 L90 90 Z' fill='none' stroke='black'/></svg>");
        using (var smooth = editor.PrepareAnchorConversion(new SKPoint(10, 10), new SKPoint(15, 0), 2))
        {
            Assert.NotNull(smooth);
            editor.ApplyAnchorConversion(smooth!);
        }
        var result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Equal(2, result.PathData.OfType<SvgCubicCurveSegment>().Count());
        Assert.Contains(result.PathData, segment => segment is SvgClosePathSegment);
        using (var corner = editor.PrepareAnchorConversion(new SKPoint(10, 10), tolerance: 2))
        {
            Assert.NotNull(corner);
            editor.ApplyAnchorConversion(corner!);
        }
        result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        var first = Assert.IsType<SvgCubicCurveSegment>(result.PathData[1]);
        var last = Assert.IsType<SvgCubicCurveSegment>(result.PathData[result.PathData.Count - 2]);
        Assert.Equal(10, first.FirstControlPoint.X, 4);
        Assert.Equal(10, first.FirstControlPoint.Y, 4);
        Assert.Equal(10, last.SecondControlPoint.X, 4);
        Assert.Equal(10, last.SecondControlPoint.Y, 4);
        Assert.Equal(2, editor.Session.UndoCount);
    }

    [Fact]
    public void StaleAnchorPreviewRejectsWithoutOverwritingAnInterveningEdit()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><path id='path' d='M10 40 L50 20 L90 40' stroke='black' fill='none'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgPath>("path"));
        using var preview = editor.PrepareAnchorConversion(new SKPoint(50, 20), new SKPoint(10, 0), 2);
        Assert.NotNull(preview);
        editor.SetStroke("red");
        var changed = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyAnchorConversion(preview!));
        Assert.Equal(changed, editor.GetSvg());
        Assert.Equal(1, editor.Session.UndoCount);
        Assert.False(editor.DeleteAnchorAt(new SKPoint(200, 200), 2));
    }
}
