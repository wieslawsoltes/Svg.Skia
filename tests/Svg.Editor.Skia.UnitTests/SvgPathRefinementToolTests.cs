using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Pathing;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgPathRefinementToolTests
{
    [Fact]
    public void SmoothBrushKeepsEndpointsAndUntouchedContoursAndCommitsOnce()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><path id='path' fill='none' stroke='red' d='M10 50 L30 20 L50 80 L70 20 L90 50 M120 30 C130 10 150 10 160 30'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("path"));
        var original = editor.GetSvg();
        using var preview = editor.PreparePathRefinement(SvgPathRefinementKind.Smooth, new[] { new SKPoint(50, 50) }, 100, 1);
        Assert.True(preview.HasChanges);
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
        Assert.True(editor.ApplyPathRefinement(preview));
        var result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Equal("path", result.ID);
        Assert.Same(SvgPaintServer.None, result.Fill);
        Assert.Contains(result.PathData, segment => segment is SvgCubicCurveSegment);
        var first = Assert.IsType<SvgMoveToSegment>(result.PathData[0]);
        Assert.Equal(10, first.End.X);
        Assert.Equal(50, first.End.Y);
        var secondContour = result.PathData.OfType<SvgMoveToSegment>().Last();
        Assert.Equal(120, secondContour.End.X);
        var last = Assert.IsType<SvgCubicCurveSegment>(result.PathData.Last());
        Assert.Equal(130, last.FirstControlPoint.X);
        Assert.Equal(10, last.FirstControlPoint.Y);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void PathEraserCutsNativeCenterlineInsteadOfExpandingItsStroke()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='160' height='100'><path id='curve' fill='none' stroke='blue' stroke-width='8' d='M10 50 C40 10 100 90 130 50'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("curve"));
        var original = editor.GetSvg();
        using var preview = editor.PreparePathRefinement(SvgPathRefinementKind.Erase, new[] { new SKPoint(70, 50) }, 8);
        Assert.True(preview.HasChanges);
        Assert.Equal(original, editor.GetSvg());
        Assert.True(editor.ApplyPathRefinement(preview));
        Assert.Equal(2, editor.SelectedElements.Count);
        Assert.All(editor.SelectedElements, element =>
        {
            var path = Assert.IsType<SvgPath>(element);
            Assert.Same(SvgPaintServer.None, path.Fill);
            Assert.Equal(8, path.StrokeWidth.Value);
            Assert.Contains(path.PathData, segment => segment is SvgCubicCurveSegment);
            Assert.DoesNotContain(path.PathData, segment => segment is SvgClosePathSegment);
        });
        Assert.Equal("curve", editor.SelectedElements[0].ID);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void PathEraserPreservesClosedSeamAndUnhitCompoundContour()
    {
        using var source = SKPath.ParseSvgPathData("M10 10H90V90H10Z M120 10L150 10");
        var pieces = SvgPathRefinementService.Erase(source, new[] { new SKPoint(50, 10) }, 5);
        Assert.NotNull(pieces);
        try
        {
            var path = Assert.Single(pieces!);
            using var measure = new SKPathMeasure(path, false);
            Assert.Equal(30, measure.Length, 2);
            Assert.True(measure.NextContour());
            Assert.False(measure.IsClosed);
            Assert.InRange(measure.Length, 309.9f, 310.1f);
            Assert.False(measure.NextContour());
        }
        finally { foreach (var piece in pieces!) piece.Dispose(); }
    }

    [Fact]
    public void JoinReversesNativeCurvesAndMapsAcrossTransformedParents()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='240' height='100' viewBox='0 0 120 50'><g transform='translate(10 0)'><path id='first' fill='none' stroke='red' d='M30 25 C25 10 10 10 0 25'/></g><path id='second' fill='none' stroke='blue' d='M50 25 Q65 40 80 25'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("first"));
        editor.Select(editor.Document.GetElementById<SvgVisualElement>("second"), true);
        var original = editor.GetSvg();
        using var preview = editor.PreparePathRefinement(SvgPathRefinementKind.Join, new[] { new SKPoint(80, 50), new SKPoint(100, 50) }, 3);
        Assert.True(preview.HasChanges);
        Assert.Equal(original, editor.GetSvg());
        editor.ApplyPathRefinement(preview);
        var joined = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Equal("first", joined.ID);
        Assert.Null(editor.Document.GetElementById<SvgVisualElement>("second"));
        Assert.Equal(255, Assert.IsType<SvgColourServer>(joined.Stroke).Colour.R);
        Assert.Single(joined.PathData.OfType<SvgMoveToSegment>());
        Assert.Contains(joined.PathData, segment => segment is SvgCubicCurveSegment);
        Assert.Contains(joined.PathData, segment => segment is SvgQuadraticCurveSegment);
        var transform = editor.GetSceneNode(joined)!.TotalTransform;
        var start = joined.PathData.First().End;
        var end = joined.PathData.Last().End;
        Assert.Equal(20, transform.MapPoint(new ShimSkiaSharp.SKPoint(start.X, start.Y)).X, 2);
        Assert.Equal(160, transform.MapPoint(new ShimSkiaSharp.SKPoint(end.X, end.Y)).X, 2);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void JoinClosesOneOpenContourAndKeepsOtherCompoundContours()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><path id='path' fill='none' stroke='black' d='M20 20L60 80L24 20 M120 20L160 20'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("path"));
        using var preview = editor.PreparePathRefinement(SvgPathRefinementKind.Join, new[] { new SKPoint(22, 20) }, 5);
        editor.ApplyPathRefinement(preview);
        var result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Equal(2, result.PathData.OfType<SvgMoveToSegment>().Count());
        Assert.Single(result.PathData.OfType<SvgClosePathSegment>());
    }

    [Fact]
    public void RefinementPreviewsRejectStaleSelectionAndDoNotTrustDisplayedGeometry()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><path id='path' fill='none' stroke='black' d='M10 50L50 10L90 50'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("path"));
        using var preview = editor.PreparePathRefinement(SvgPathRefinementKind.Smooth, new[] { new SKPoint(50, 10) }, 20, 1);
        Assert.True(preview.HasChanges);
        preview.Paths[0].Transform(SKMatrix.CreateTranslation(1000, 1000));
        editor.ApplyPathRefinement(preview);
        Assert.True(editor.SelectionBounds!.Value.Right < 100);
        Assert.Throws<InvalidOperationException>(() => editor.ApplyPathRefinement(preview));
        using var stale = editor.PreparePathRefinement(SvgPathRefinementKind.Erase, new[] { new SKPoint(50, 30) }, 10);
        editor.Select(null);
        Assert.Throws<InvalidOperationException>(() => editor.ApplyPathRefinement(stale));
    }

    [Fact]
    public void EmptyAndDistantRefinementLeavesHistoryAndPrimitiveTypesUntouched()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect id='rect' x='10' y='10' width='30' height='30'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("rect"));
        var original = editor.GetSvg();
        foreach (var kind in Enum.GetValues<SvgPathRefinementKind>())
        {
            using var preview = editor.PreparePathRefinement(kind, new[] { new SKPoint(500, 500) }, 10);
            Assert.False(preview.HasChanges);
            Assert.False(editor.ApplyPathRefinement(preview));
        }
        Assert.Equal(original, editor.GetSvg());
        Assert.IsType<SvgRectangle>(editor.SelectedElements[0]);
        Assert.False(editor.CanUndo);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.PreparePathRefinement(SvgPathRefinementKind.Smooth, new[] { new SKPoint(20, 20) }, 0));
    }

    [Fact]
    public void RefinementUsesRetainedPercentageGeometryWithoutChangingTheClip()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='400' height='200' viewBox='0 0 200 100'><defs><clipPath id='clip'><rect width='200' height='100'/></clipPath></defs><rect id='rect' x='10%' y='10%' width='50%' height='50%' clip-path='url(#clip)'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("rect"));
        using var preview = editor.PreparePathRefinement(SvgPathRefinementKind.Smooth, new[] { new SKPoint(40, 20) }, 8, 1);
        Assert.True(preview.HasChanges);
        editor.ApplyPathRefinement(preview);
        var result = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Contains("clip", result.ClipPath.OriginalString);
        using var geometry = PathService.ElementToPath(result)!;
        Assert.True(geometry.Bounds.Right > 100);
        Assert.True(geometry.Bounds.Bottom > 50);
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void BlobBrushProducesNativeFillAndMergesOnlyCompatibleTouchingColors()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect id='blue' x='10' y='20' width='40' height='40' fill='blue' stroke='none'/><rect id='red' x='80' y='20' width='40' height='40' fill='red' stroke='none'/></svg>");
        editor.CurrentFill = "blue";
        var original = editor.GetSvg();
        var path = editor.AddBlobStroke(new[] { new SKPoint(30, 40), new SKPoint(100, 40) }, 20);
        Assert.Equal("blue", path.ID);
        Assert.Same(SvgPaintServer.None, path.Stroke);
        Assert.Equal(255, Assert.IsType<SvgColourServer>(path.Fill).Colour.B);
        Assert.NotNull(editor.Document!.GetElementById<SvgVisualElement>("red"));
        Assert.Equal(2, editor.Document.Children.OfType<SvgVisualElement>().Count());
        using var geometry = PathService.ElementToPath(path)!;
        Assert.True(geometry.Contains(30, 25));
        Assert.True(geometry.Contains(95, 40));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void BlobBrushClickAndStrokeUsePictureCoordinatesInImportedViewBox()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='400' height='200' viewBox='10 20 200 100'/>");
        editor.CurrentFill = "#0088FF";
        var path = editor.AddBlobStroke(new[] { new SKPoint(100, 80) }, 20, false);
        var bounds = editor.GetSceneNode(path)!.TransformedBounds;
        Assert.Equal(90, bounds.Left, 2);
        Assert.Equal(70, bounds.Top, 2);
        Assert.Equal(110, bounds.Right, 2);
        Assert.Equal(90, bounds.Bottom, 2);
        Assert.Same(SvgPaintServer.None, path.Stroke);
        Assert.Single(path.PathData.OfType<SvgClosePathSegment>());
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void MeasurementUsesRootUnitsAndDoesNotChangeSelectionOrHistory()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='400' height='200' viewBox='10 20 200 100'><rect id='rect' x='10' y='20' width='10' height='10'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("rect"));
        var original = editor.GetSvg();
        var measurement = editor.MeasureInDocumentCoordinates(new SKPoint(20, 20), new SKPoint(80, 100));
        Assert.Equal(30, measurement.DeltaX, 4);
        Assert.Equal(40, measurement.DeltaY, 4);
        Assert.Equal(50, measurement.Distance, 4);
        Assert.Equal(53.130102, measurement.AngleDegrees, 5);
        Assert.Equal(new SKPoint(20, 30), measurement.Start);
        Assert.Equal(original, editor.GetSvg());
        Assert.Equal("rect", Assert.Single(editor.SelectedElements).ID);
        Assert.False(editor.CanUndo);
    }

    [Theory]
    [InlineData(20, 0)]
    [InlineData(0, 20)]
    public void ShearKeepsTheSelectionCenterAndAreaWhileConstrainingItsAngle(float dx, float dy)
    {
        var bounds = new SKRect(10, 20, 110, 80);
        var center = new SKPoint(bounds.MidX, bounds.MidY);
        var transform = SvgEditorToolGeometry.CreateTransform(SvgEditorInteractionMode.Shear, bounds, center,
            new SKPoint(center.X + dx, center.Y + dy), true);
        Assert.Equal(center.X, transform.MapPoint(center).X, 4);
        Assert.Equal(center.Y, transform.MapPoint(center).Y, 4);
        Assert.Equal(1, transform.ScaleX * transform.ScaleY - transform.SkewX * transform.SkewY, 5);
        var shear = dx != 0 ? transform.SkewX : transform.SkewY;
        var angle = Math.Atan(shear) * 180 / Math.PI;
        Assert.Equal(Math.Round(angle / 15) * 15, angle, 4);
        Assert.True(transform.TryInvert(out _));
    }

    [Fact]
    public void ShapeOptionsChangePreviewGeometryAndCommittedGeometryTogether()
    {
        using var square = SvgEditorToolGeometry.CreateShape(SvgEditorInteractionMode.RoundedRectangle, new SKPoint(10, 10), new SKPoint(90, 70), 6, 0, 0.42f);
        using var rounded = SvgEditorToolGeometry.CreateShape(SvgEditorInteractionMode.RoundedRectangle, new SKPoint(10, 10), new SKPoint(90, 70), 6, 0.5f, 0.42f);
        Assert.True(square.Contains(12, 12));
        Assert.False(rounded.Contains(12, 12));
        using var editor = new SvgEditorController();
        editor.NewDocument(100, 100);
        var path = editor.AddEditorShape(SvgEditorInteractionMode.Star, new SKPoint(10, 10), new SKPoint(90, 90), 5, 0.15f, 0.8f);
        using var expected = SvgEditorToolGeometry.CreateShape(SvgEditorInteractionMode.Star, new SKPoint(10, 10), new SKPoint(90, 90), 5, 0.15f, 0.8f);
        using var actual = PathService.ElementToPath(path)!;
        using var difference = actual.Op(expected, SKPathOp.Xor);
        Assert.NotNull(difference);
        Assert.True(difference!.IsEmpty);
    }
}
