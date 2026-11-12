using System;
using System.Linq;
using System.Threading;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgPathDeformationTests
{
    [Theory]
    [InlineData(SvgDeformationKind.Warp)]
    [InlineData(SvgDeformationKind.Twirl)]
    [InlineData(SvgDeformationKind.Pucker)]
    [InlineData(SvgDeformationKind.Bloat)]
    [InlineData(SvgDeformationKind.Scallop)]
    [InlineData(SvgDeformationKind.Crystallize)]
    [InlineData(SvgDeformationKind.Wrinkle)]
    public void EveryBrushProducesFiniteDistinctGeometryWithoutChangingItsSource(SvgDeformationKind kind)
    {
        using var source = SourcePath();
        var before = PathService.ToSvgPathData(source);
        using var result = SvgPathDeformationService.Deform(source, kind,
            new[] { new SKPoint(0, 0), new SKPoint(10, 8) },
            new SvgDeformationOptions { Radius = 80, Strength = 0.55f, Detail = 7 }, out var changed);
        Assert.True(changed);
        Assert.NotEqual(before, PathService.ToSvgPathData(result));
        Assert.Equal(before, PathService.ToSvgPathData(source));
        Assert.Equal(source.FillType, result.FillType);
        Assert.All(result.Points, point =>
        {
            Assert.True(float.IsFinite(point.X));
            Assert.True(float.IsFinite(point.Y));
        });
        using var measure = new SKPathMeasure(result);
        Assert.True(measure.IsClosed);
    }

    [Fact]
    public void UntouchedOrZeroStrengthPathsRetainTheirExactCurves()
    {
        using var source = SourcePath();
        var before = PathService.ToSvgPathData(source);
        using var outside = SvgPathDeformationService.Deform(source, SvgDeformationKind.Wrinkle,
            new[] { new SKPoint(5000, 5000) }, null, out var outsideChanged);
        using var zero = SvgPathDeformationService.Deform(source, SvgDeformationKind.Twirl,
            new[] { new SKPoint(0, 0) }, new SvgDeformationOptions { Strength = 0 }, out var zeroChanged);
        using var stationary = SvgPathDeformationService.Deform(source, SvgDeformationKind.Warp,
            new[] { new SKPoint(0, 0) }, null, out var stationaryChanged);
        Assert.False(outsideChanged || zeroChanged || stationaryChanged);
        Assert.Equal(before, PathService.ToSvgPathData(outside));
        Assert.Equal(before, PathService.ToSvgPathData(zero));
        Assert.Equal(before, PathService.ToSvgPathData(stationary));
    }

    [Fact]
    public void LongStraightSegmentsAreSubdividedInsideTheBrushAndKeepTheirDistantEndpoints()
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(-100, 0);
        builder.LineTo(100, 0);
        using var source = builder.Detach();
        using var result = SvgPathDeformationService.Deform(source, SvgDeformationKind.Warp,
            new[] { new SKPoint(0, 0), new SKPoint(0, 15) },
            new SvgDeformationOptions { Radius = 20, Strength = 1 }, out var changed);
        Assert.True(changed);
        Assert.Equal(new SKPoint(-100, 0), result.Points[0]);
        Assert.Equal(new SKPoint(100, 0), result.LastPoint);
        Assert.Contains(result.Points, point => point.Y > 1);
        using var measure = new SKPathMeasure(result);
        Assert.False(measure.IsClosed);
    }

    [Fact]
    public void TwirlPreservesRadiusAndPuckerAndBloatMoveInOppositeDirections()
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(10, 0);
        builder.LineTo(20, 0);
        using var source = builder.Detach();
        var options = new SvgDeformationOptions { Radius = 50, Strength = 0.5f };
        var stroke = new[] { new SKPoint(0, 0) };
        using var twirl = SvgPathDeformationService.Deform(source, SvgDeformationKind.Twirl, stroke, options, out _);
        using var pucker = SvgPathDeformationService.Deform(source, SvgDeformationKind.Pucker, stroke, options, out _);
        using var bloat = SvgPathDeformationService.Deform(source, SvgDeformationKind.Bloat, stroke, options, out _);
        Assert.InRange(Math.Abs(SKPoint.Distance(default, twirl.LastPoint) - 20), 0, 0.001f);
        Assert.True(twirl.LastPoint.Y > 0);
        Assert.True(pucker.LastPoint.X < 20);
        Assert.True(bloat.LastPoint.X > 20);
    }

    [Fact]
    public void BrushBoundaryHasZeroDisplacementAndNegativeStrengthReversesTwirl()
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(50, 0);
        builder.LineTo(50, 20);
        using var boundary = builder.Detach();
        using var unchanged = SvgPathDeformationService.Deform(boundary, SvgDeformationKind.Bloat,
            new[] { new SKPoint(0, 0) }, new SvgDeformationOptions { Radius = 50 }, out var changed);
        Assert.False(changed);
        using var lineBuilder = new SKPathBuilder();
        lineBuilder.MoveTo(10, 0);
        lineBuilder.LineTo(20, 0);
        using var line = lineBuilder.Detach();
        using var reversed = SvgPathDeformationService.Deform(line, SvgDeformationKind.Twirl,
            new[] { new SKPoint(0, 0) }, new SvgDeformationOptions { Radius = 50, Strength = -0.5f }, out _);
        Assert.True(reversed.LastPoint.Y < 0);
    }

    [Fact]
    public void ResamplingMakesTheSameStrokeIndependentOfPointerPacketDensity()
    {
        using var source = SourcePath();
        var options = new SvgDeformationOptions { Radius = 40 };
        using var sparse = SvgPathDeformationService.Deform(source, SvgDeformationKind.Warp,
            new[] { new SKPoint(0, 0), new SKPoint(20, 0) }, options, out _);
        using var dense = SvgPathDeformationService.Deform(source, SvgDeformationKind.Warp,
            new[] { new SKPoint(0, 0), new SKPoint(5, 0), new SKPoint(10, 0), new SKPoint(15, 0), new SKPoint(20, 0) }, options, out _);
        Assert.Equal(PathService.ToSvgPathData(sparse), PathService.ToSvgPathData(dense));
    }

    [Theory]
    [InlineData(SvgDeformationKind.Twirl)]
    [InlineData(SvgDeformationKind.Pucker)]
    [InlineData(SvgDeformationKind.Bloat)]
    [InlineData(SvgDeformationKind.Scallop)]
    [InlineData(SvgDeformationKind.Crystallize)]
    [InlineData(SvgDeformationKind.Wrinkle)]
    public void HeldBrushAccumulatesAtAStationaryPointer(SvgDeformationKind kind)
    {
        using var source = SourcePath();
        var settings = new SvgDeformationOptions { Radius = 80, Strength = 0.2f };
        using var contact = SvgPathDeformationService.Deform(source, kind,
            new[] { new SvgDeformationSample(default, 0) }, settings, out var contactChanged);
        using var held = SvgPathDeformationService.Deform(source, kind,
            new[] { new SvgDeformationSample(default, 0), new SvgDeformationSample(default, 1) }, settings, out var heldChanged);
        Assert.True(contactChanged && heldChanged);
        Assert.NotEqual(PathService.ToSvgPathData(contact), PathService.ToSvgPathData(held));
        Assert.Equal(source.FillType, held.FillType);
        Assert.All(held.Points, point => Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y)));
    }

    [Theory]
    [InlineData(SvgDeformationKind.Twirl)]
    [InlineData(SvgDeformationKind.Pucker)]
    [InlineData(SvgDeformationKind.Bloat)]
    [InlineData(SvgDeformationKind.Scallop)]
    [InlineData(SvgDeformationKind.Crystallize)]
    [InlineData(SvgDeformationKind.Wrinkle)]
    public void TimedInterpolationDoesNotDependOnPointerOrPreviewFrequency(SvgDeformationKind kind)
    {
        using var source = SourcePath();
        var settings = new SvgDeformationOptions { Radius = 80, Strength = 0.2f };
        var sparseStroke = new[] { new SvgDeformationSample(default, 0), new SvgDeformationSample(new SKPoint(20, 10), 1) };
        var denseStroke = Enumerable.Range(0, 101).Select(index =>
            new SvgDeformationSample(new SKPoint(index * 0.2f, index * 0.1f), index / 100d)).ToArray();
        using var sparse = SvgPathDeformationService.Deform(source, kind, sparseStroke, settings, out _);
        using var dense = SvgPathDeformationService.Deform(source, kind, denseStroke, settings, out _);
        Assert.Equal(sparse.PointCount, dense.PointCount);
        for (var index = 0; index < sparse.PointCount; index++)
            Assert.InRange(SKPoint.Distance(sparse.Points[index], dense.Points[index]), 0, 0.0001f);
    }

    [Fact]
    public void WarpIgnoresElapsedHoldAndZeroDwellRatePreservesOnlyInitialContact()
    {
        using var source = SourcePath();
        var settings = new SvgDeformationOptions { Radius = 40, DwellRate = 0 };
        using var spatial = SvgPathDeformationService.Deform(source, SvgDeformationKind.Warp,
            new[] { new SKPoint(0, 0), new SKPoint(20, 0) }, settings, out _);
        using var timed = SvgPathDeformationService.Deform(source, SvgDeformationKind.Warp,
            new[] { new SvgDeformationSample(default, 0), new SvgDeformationSample(new SKPoint(20, 0), 0.2),
                new SvgDeformationSample(new SKPoint(20, 0), 3) }, settings, out _);
        Assert.Equal(PathService.ToSvgPathData(spatial), PathService.ToSvgPathData(timed));
        using var stationary = SvgPathDeformationService.Deform(source, SvgDeformationKind.Warp,
            new[] { new SvgDeformationSample(default, 0), new SvgDeformationSample(default, 3) }, settings, out var moved);
        Assert.False(moved);
        Assert.Equal(PathService.ToSvgPathData(source), PathService.ToSvgPathData(stationary));
        using var contact = SvgPathDeformationService.Deform(source, SvgDeformationKind.Pucker,
            new[] { new SKPoint(0, 0) }, settings, out _);
        using var held = SvgPathDeformationService.Deform(source, SvgDeformationKind.Pucker,
            new[] { new SvgDeformationSample(default, 0), new SvgDeformationSample(default, 3) }, settings, out _);
        Assert.Equal(PathService.ToSvgPathData(contact), PathService.ToSvgPathData(held));
    }

    [Fact]
    public void InvalidTimeSequencesAndDwellRatesCannotChangeTheDocument()
    {
        using var editor = EditorWithPath();
        var before = editor.GetSvg();
        var center = new SKPoint(30, 30);
        foreach (var times in new[] { new[] { 1d }, new[] { 0d, double.NaN }, new[] { 0d, double.PositiveInfinity },
            new[] { 0d, -1d }, new[] { 0d, 0.3, 0.2 }, new[] { 0d, SvgPathDeformationService.MaximumDurationSeconds + 1 } })
        {
            var samples = times.Select(time => new SvgDeformationSample(center, time)).ToArray();
            Assert.ThrowsAny<ArgumentException>(() => editor.PrepareDeformation(SvgDeformationKind.Twirl, samples));
        }
        var valid = new[] { new SvgDeformationSample(center, 0), new SvgDeformationSample(center, 1) };
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.PrepareDeformation(SvgDeformationKind.Twirl, valid,
            new SvgDeformationOptions { DwellRate = float.NaN }));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => editor.PrepareDeformation(SvgDeformationKind.Twirl, valid, cancellationToken: cancel.Token));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void ATimeIntegratedPreviewCommitsOnceAndUndoesExactly()
    {
        using var editor = EditorWithPath();
        var before = editor.GetSvg();
        var samples = new[] { new SvgDeformationSample(new SKPoint(30, 30), 0), new SvgDeformationSample(new SKPoint(30, 30), 0.37) };
        using var preview = editor.PrepareDeformation(SvgDeformationKind.Twirl, samples);
        Assert.True(preview.HasChanges);
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
        Assert.True(editor.ApplyDeformation(preview));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void PreparationAndDiscardDoNotWriteHistoryAndCommitPreservesViewBoxTransformAndIdentity()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="10 20 100 50">
              <g id="layer" transform="translate(20 25) scale(2 1)" fill="red">
                <rect id="target" width="10" height="10" stroke="blue" stroke-width="0.5"/>
              </g>
            </svg>
            """);
        var target = editor.Document!.GetElementById<SvgRectangle>("target");
        var fill = target.Fill.ToString();
        var strokePaint = target.Stroke.ToString();
        editor.Select(target);
        var before = editor.GetSvg();
        var revision = editor.RenderRevision;
        var stroke = new[] { new SKPoint(35, 15), new SKPoint(35, 25) };
        using (var discarded = editor.PrepareDeformation(SvgDeformationKind.Warp, stroke, new SvgDeformationOptions { Radius = 35 }))
            Assert.True(discarded.HasChanges);
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal(revision, editor.RenderRevision);
        Assert.False(editor.CanUndo);
        using var prepared = editor.PrepareDeformation(SvgDeformationKind.Warp, stroke, new SvgDeformationOptions { Radius = 35 });
        var previewBounds = Assert.Single(prepared.Paths).Bounds;
        Assert.True(editor.ApplyDeformation(prepared));
        var path = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Equal("target", path.ID);
        Assert.Equal("layer", path.Parent!.ID);
        Assert.Equal(fill, path.Fill.ToString());
        Assert.Equal(strokePaint, path.Stroke.ToString());
        var actual = editor.GetSceneNode(path)!.TransformedBounds;
        Assert.InRange(Math.Abs(actual.Left - previewBounds.Left), 0, 0.01f);
        Assert.InRange(Math.Abs(actual.Bottom - previewBounds.Bottom), 0, 0.01f);
        Assert.Equal(1, editor.Session.UndoCount);
        var after = editor.GetSvg();
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
        editor.Redo();
        Assert.Equal(after, editor.GetSvg());
    }

    [Fact]
    public void ASelectedGroupKeepsItsSelectionAndUnaffectedPrimitivesStayNative()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="400" height="100">
              <g id="group"><circle id="near" cx="30" cy="30" r="20"/><rect id="far" x="300" y="20" width="20" height="20"/></g>
            </svg>
            """);
        var group = editor.Document!.GetElementById<SvgGroup>("group");
        editor.Select(group);
        Assert.True(editor.DeformAlong(SvgDeformationKind.Wrinkle, new[] { new SKPoint(30, 30) }));
        Assert.Same(group, Assert.Single(editor.SelectedElements));
        Assert.IsType<SvgPath>(editor.Document.GetElementById("near"));
        Assert.IsType<SvgRectangle>(editor.Document.GetElementById("far"));
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void StaleDisposedAlreadyAppliedAndForeignPreviewsCannotMutateTheDocument()
    {
        using var editor = EditorWithPath();
        using var prepared = editor.PrepareDeformation(SvgDeformationKind.Twirl, new[] { new SKPoint(30, 30) });
        using var other = EditorWithPath();
        Assert.Throws<InvalidOperationException>(() => other.ApplyDeformation(prepared));
        var before = editor.GetSvg();
        editor.ClearSelection();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyDeformation(prepared));
        Assert.Equal(before, editor.GetSvg());
        editor.SelectAll();
        var disposed = editor.PrepareDeformation(SvgDeformationKind.Twirl, new[] { new SKPoint(30, 30) });
        disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => editor.ApplyDeformation(disposed));
        using var valid = editor.PrepareDeformation(SvgDeformationKind.Wrinkle, new[] { new SKPoint(30, 30) });
        Assert.True(editor.ApplyDeformation(valid));
        Assert.Throws<InvalidOperationException>(() => editor.ApplyDeformation(valid));
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void PreviewDrawingCannotChangeThePreparedLocalPathData()
    {
        using var editor = EditorWithPath();
        using var prepared = editor.PrepareDeformation(SvgDeformationKind.Wrinkle, new[] { new SKPoint(30, 30) });
        // Native preview handles are exposed for drawing only. Even accidental
        // drawing-side mutation must not become an unreviewed document edit.
        prepared.Paths[0].Transform(SKMatrix.CreateTranslation(10000, 10000));
        Assert.True(editor.ApplyDeformation(prepared));
        Assert.InRange(editor.SelectionBounds!.Value.Right, 0, 100);
    }

    [Fact]
    public void InvalidGeometryBudgetsCancellationAndLocksFailBeforeAnyMutation()
    {
        using var editor = EditorWithPath();
        var before = editor.GetSvg();
        var stroke = new[] { new SKPoint(30, 30) };
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.PrepareDeformation(SvgDeformationKind.Twirl, stroke, new SvgDeformationOptions { Radius = 0 }));
        Assert.Throws<ArgumentException>(() => editor.PrepareDeformation(SvgDeformationKind.Twirl, new[] { new SKPoint(float.NaN, 0) }));
        Assert.Throws<InvalidOperationException>(() => editor.PrepareDeformation(SvgDeformationKind.Twirl, stroke, new SvgDeformationOptions { MaximumPoints = 16, Radius = 50 }));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => editor.PrepareDeformation(SvgDeformationKind.Twirl, stroke, cancellationToken: cancel.Token));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
        editor.SelectedElements[0].CustomAttributes["data-lock"] = "true";
        Assert.Throws<InvalidOperationException>(() => editor.PrepareDeformation(SvgDeformationKind.Twirl, stroke));
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void ASelectionContainingUnsupportedArtworkIsRejectedAtomically()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect width='60' height='60'/><text x='90' y='50'>Text</text></svg>");
        editor.SelectAll();
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.PrepareDeformation(SvgDeformationKind.Warp,
            new[] { new SKPoint(30, 30), new SKPoint(30, 50) }));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    private static SKPath SourcePath()
    {
        using var builder = new SKPathBuilder { FillType = SKPathFillType.EvenOdd };
        builder.MoveTo(-30, 0);
        builder.CubicTo(-10, -20, 10, 20, 30, 0);
        builder.LineTo(25, 30);
        builder.LineTo(-15, 30);
        builder.Close();
        return builder.Detach();
    }

    private static SvgEditorController EditorWithPath()
    {
        var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><path id='target' d='M10 10H60V60H10Z'/></svg>");
        editor.SelectAll();
        return editor;
    }
}
