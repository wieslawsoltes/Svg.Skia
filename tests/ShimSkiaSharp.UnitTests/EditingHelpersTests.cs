using System;
using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class EditingHelpersTests
{
    [Fact]
    public void FindCommands_ReturnsMatchingCommands()
    {
        var picture = new SKPicture(
            SKRect.Create(0, 0, 10, 10),
            new List<CanvasCommand>
            {
                new DrawPathCanvasCommand(CloneTestData.CreatePath(), CloneTestData.CreatePaint()),
                new SaveCanvasCommand(1)
            });

        var commands = picture.FindCommands<DrawPathCanvasCommand>().ToList();

        Assert.Single(commands);
        Assert.IsType<DrawPathCanvasCommand>(commands[0]);
    }

    [Fact]
    public void ReplaceCommands_RemovesAndReplaces()
    {
        var originalPath = CloneTestData.CreatePath();
        var newPath = CloneTestData.CreatePath();
        var commands = new List<CanvasCommand>
        {
            new DrawTextCanvasCommand("Text", 1, 2, null),
            new DrawPathCanvasCommand(originalPath, null)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);

        var replaced = picture.ReplaceCommands(command =>
            command is DrawTextCanvasCommand
                ? null
                : new DrawPathCanvasCommand(newPath, null));

        Assert.Equal(2, replaced);
        Assert.Single(commands);
        var drawPath = Assert.IsType<DrawPathCanvasCommand>(commands[0]);
        Assert.Same(newPath, drawPath.Path);
    }

    [Fact]
    public void UpdatePaints_InPlace_UpdatesUniquePaints()
    {
        var sharedPaint = CloneTestData.CreatePaint();
        var commands = new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(CloneTestData.CreatePath(), sharedPaint),
            new DrawImageCanvasCommand(CloneTestData.CreateImage(), SKRect.Create(0, 0, 1, 1), SKRect.Create(0, 0, 1, 1), sharedPaint)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);

        var updated = picture.UpdatePaints(
            paint => paint.Color is { },
            paint => paint.Color = new SKColor(9, 9, 9, 9));

        Assert.Equal(1, updated);
        Assert.Equal(new SKColor(9, 9, 9, 9), sharedPaint.Color);
        Assert.Same(sharedPaint, ((DrawPathCanvasCommand)commands[0]).Paint);
    }

    [Fact]
    public void UpdatePaints_CloneOnWrite_SharedPaintUsesClone()
    {
        var sharedPaint = CloneTestData.CreatePaint();
        var commands = new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(CloneTestData.CreatePath(), sharedPaint),
            new DrawPathCanvasCommand(CloneTestData.CreatePath(), sharedPaint)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);

        var updated = picture.UpdatePaints(
            paint => paint.Color is { },
            paint => paint.Color = new SKColor(7, 7, 7, 7),
            EditMode.CloneOnWrite);

        Assert.Equal(1, updated);
        var first = ((DrawPathCanvasCommand)commands[0]).Paint!;
        var second = ((DrawPathCanvasCommand)commands[1]).Paint!;
        Assert.NotSame(sharedPaint, first);
        Assert.Same(first, second);
        Assert.Equal(new SKColor(1, 2, 3, 4), sharedPaint.Color);
        Assert.Equal(new SKColor(7, 7, 7, 7), first.Color);
    }

    [Fact]
    public void UpdatePaints_CloneOnWrite_LeavesNonMatchingPaints()
    {
        var paintA = CloneTestData.CreatePaint();
        var paintB = CloneTestData.CreatePaint();
        var commands = new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(CloneTestData.CreatePath(), paintA),
            new DrawPathCanvasCommand(CloneTestData.CreatePath(), paintB)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);

        var updated = picture.UpdatePaints(
            paint => ReferenceEquals(paint, paintA),
            paint => paint.Color = new SKColor(4, 4, 4, 4),
            EditMode.CloneOnWrite);

        Assert.Equal(1, updated);
        var updatedPaint = ((DrawPathCanvasCommand)commands[0]).Paint!;
        var untouchedPaint = ((DrawPathCanvasCommand)commands[1]).Paint!;
        Assert.NotSame(paintA, updatedPaint);
        Assert.Same(paintB, untouchedPaint);
    }

    [Fact]
    public void UpdatePaths_InPlace_UpdatesUniquePaths()
    {
        var sharedPath = CloneTestData.CreatePath();
        var commands = new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(sharedPath, null),
            new DrawTextOnPathCanvasCommand("Text", sharedPath, 0, 0, null)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);
        var originalCount = sharedPath.Commands!.Count;

        var updated = picture.UpdatePaths(
            path => ReferenceEquals(path, sharedPath),
            path => path.LineTo(9, 9));

        Assert.Equal(1, updated);
        Assert.Equal(originalCount + 1, sharedPath.Commands!.Count);
        Assert.Same(sharedPath, ((DrawPathCanvasCommand)commands[0]).Path);
    }

    [Fact]
    public void UpdatePaths_CloneOnWrite_ClonesSharedPath()
    {
        var sharedPath = CloneTestData.CreatePath();
        var commands = new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(sharedPath, null),
            new DrawPathCanvasCommand(sharedPath, null)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);
        var originalCount = sharedPath.Commands!.Count;

        var updated = picture.UpdatePaths(
            path => ReferenceEquals(path, sharedPath),
            path => path.LineTo(9, 9),
            EditMode.CloneOnWrite);

        Assert.Equal(1, updated);
        var first = ((DrawPathCanvasCommand)commands[0]).Path!;
        var second = ((DrawPathCanvasCommand)commands[1]).Path!;
        Assert.NotSame(sharedPath, first);
        Assert.Same(first, second);
        Assert.Equal(originalCount, sharedPath.Commands!.Count);
        Assert.Equal(originalCount + 1, first.Commands!.Count);
    }

    [Fact]
    public void UpdatePaths_CloneOnWrite_UpdatesClipPaths()
    {
        var clipPath = CloneTestData.CreateClipPath();
        var commands = new List<CanvasCommand>
        {
            new ClipPathCanvasCommand(clipPath, SKClipOperation.Intersect, false)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);

        var updated = picture.UpdatePaths(
            path => path.Commands is { Count: > 0 },
            path => path.LineTo(5, 5),
            EditMode.CloneOnWrite);

        var updatedClip = ((ClipPathCanvasCommand)commands[0]).ClipPath!;
        Assert.NotSame(clipPath, updatedClip);
        Assert.Equal(2, updated);
    }

    [Fact]
    public void UpdatePaths_CloneOnWrite_SharedPathUpdatesOnce()
    {
        var sharedPath = CloneTestData.CreatePath();
        var clipPath = new ClipPath();
        clipPath.Clips!.Add(new PathClip { Path = sharedPath });
        var commands = new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(sharedPath, null),
            new ClipPathCanvasCommand(clipPath, SKClipOperation.Intersect, false)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);
        var originalCount = sharedPath.Commands!.Count;

        var updated = picture.UpdatePaths(
            path => ReferenceEquals(path, sharedPath),
            path => path.LineTo(5, 5),
            EditMode.CloneOnWrite);

        Assert.Equal(1, updated);
        var updatedDrawPath = ((DrawPathCanvasCommand)commands[0]).Path!;
        var updatedClipPath = ((ClipPathCanvasCommand)commands[1]).ClipPath!;
        var updatedClipPathPath = updatedClipPath.Clips![0].Path!;
        Assert.NotSame(sharedPath, updatedDrawPath);
        Assert.Same(updatedDrawPath, updatedClipPathPath);
        Assert.Equal(originalCount + 1, updatedDrawPath.Commands!.Count);
        Assert.Equal(originalCount, sharedPath.Commands!.Count);
    }

    [Fact]
    public void PathUpdateCommands_ReplacesMatchingCommands()
    {
        var path = new SKPath();
        path.MoveTo(1, 2);
        path.LineTo(3, 4);

        var updated = path.UpdateCommands(
            command => command is MoveToPathCommand,
            command => new MoveToPathCommand(9, 10));

        Assert.Equal(1, updated);
        var move = Assert.IsType<MoveToPathCommand>(path.Commands![0]);
        Assert.Equal(9, move.X);
        Assert.Equal(10, move.Y);
    }

    [Fact]
    public void PathTransform_MapsCommands()
    {
        var path = new SKPath();
        path.MoveTo(1, 2);
        path.LineTo(3, 4);
        path.AddCircle(1, 1, 2);

        var matrix = SKMatrix.CreateTranslation(2, 3);
        path.Transform(matrix);

        var move = Assert.IsType<MoveToPathCommand>(path.Commands![0]);
        var line = Assert.IsType<LineToPathCommand>(path.Commands![1]);
        var circle = Assert.IsType<AddCirclePathCommand>(path.Commands![2]);
        Assert.Equal(3, move.X);
        Assert.Equal(5, move.Y);
        Assert.Equal(5, line.X);
        Assert.Equal(7, line.Y);
        Assert.Equal(3, circle.X);
        Assert.Equal(4, circle.Y);
    }

    [Fact]
    public void PathTransform_ConvertsCircleToOvalOnNonUniformScale()
    {
        var path = new SKPath();
        path.AddCircle(1, 1, 2);

        var matrix = SKMatrix.CreateScale(2, 3);
        path.Transform(matrix);

        Assert.IsType<AddOvalPathCommand>(path.Commands![0]);
    }

    [Fact]
    public void PathTransform_ThrowsOnRotation()
    {
        var path = new SKPath();
        path.MoveTo(1, 2);

        Assert.Throws<NotSupportedException>(() => path.Transform(SKMatrix.CreateRotationDegrees(90)));
    }

    [Fact]
    public void PathTransform_FlipsArcSweepOnReflection()
    {
        var path = new SKPath();
        path.ArcTo(2, 3, 0, SKPathArcSize.Small, SKPathDirection.Clockwise, 4, 5);

        path.Transform(SKMatrix.CreateScale(-1, 1));

        var arc = Assert.IsType<ArcToPathCommand>(path.Commands![0]);
        Assert.Equal(SKPathDirection.CounterClockwise, arc.Sweep);
    }

    [Fact]
    public void ApplyColorTransform_UpdatesColor()
    {
        var paint = CloneTestData.CreatePaint();

        paint.ApplyColorTransform(color => new SKColor(8, 8, 8, color.Alpha));

        Assert.Equal(new SKColor(8, 8, 8, 4), paint.Color);
    }

    [Fact]
    public void ApplyShaderTransform_UpdatesShader()
    {
        var paint = CloneTestData.CreatePaint();
        var newShader = SKShader.CreateColor(new SKColor(9, 9, 9, 9), SKColorSpace.Srgb);

        paint.ApplyShaderTransform(_ => newShader);

        Assert.Same(newShader, paint.Shader);
    }

    [Fact]
    public void CanvasCommandVisitor_VisitsExpectedCommand()
    {
        var visited = new List<string>();
        var visitor = new TrackingVisitor(visited);
        var command = new DrawTextCanvasCommand("Text", 1, 2, null);

        command.Accept(visitor);

        Assert.Single(visited);
        Assert.Equal(nameof(DrawTextCanvasCommand), visited[0]);
    }

    private sealed class TrackingVisitor : ICanvasCommandVisitor
    {
        private readonly IList<string> _visited;

        public TrackingVisitor(IList<string> visited) => _visited = visited;

        public void Visit(ClipPathCanvasCommand cmd) => _visited.Add(nameof(ClipPathCanvasCommand));
        public void Visit(ClipRectCanvasCommand cmd) => _visited.Add(nameof(ClipRectCanvasCommand));
        public void Visit(DrawImageCanvasCommand cmd) => _visited.Add(nameof(DrawImageCanvasCommand));
        public void Visit(DrawPathCanvasCommand cmd) => _visited.Add(nameof(DrawPathCanvasCommand));
        public void Visit(DrawTextBlobCanvasCommand cmd) => _visited.Add(nameof(DrawTextBlobCanvasCommand));
        public void Visit(DrawTextCanvasCommand cmd) => _visited.Add(nameof(DrawTextCanvasCommand));
        public void Visit(DrawTextOnPathCanvasCommand cmd) => _visited.Add(nameof(DrawTextOnPathCanvasCommand));
        public void Visit(RestoreCanvasCommand cmd) => _visited.Add(nameof(RestoreCanvasCommand));
        public void Visit(SaveCanvasCommand cmd) => _visited.Add(nameof(SaveCanvasCommand));
        public void Visit(SaveLayerCanvasCommand cmd) => _visited.Add(nameof(SaveLayerCanvasCommand));
        public void Visit(SetMatrixCanvasCommand cmd) => _visited.Add(nameof(SetMatrixCanvasCommand));
    }
}
