using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class SKCanvasTests
{
    private static SKCanvas CreateCanvas()
    {
        var recorder = new SKPictureRecorder();
        return recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
    }

    [Fact]
    public void SetMatrix_AddsCommandAndUpdatesMatrix()
    {
        var canvas = CreateCanvas();
        var delta = SKMatrix.CreateTranslation(5, 5);
        canvas.SetMatrix(delta);
        Assert.Equal(delta, canvas.TotalMatrix);
        var cmd = Assert.IsType<SetMatrixCanvasCommand>(canvas.Commands!.Single());
        Assert.Equal(delta, cmd.DeltaMatrix);
        Assert.Equal(delta, cmd.TotalMatrix);
    }

    [Fact]
    public void SaveAndRestore_RecordCommands()
    {
        var canvas = CreateCanvas();
        var count = canvas.Save();
        Assert.Equal(1, count);
        canvas.Restore();
        Assert.Equal(2, canvas.Commands!.Count);
        var save = Assert.IsType<SaveCanvasCommand>(canvas.Commands![0]);
        Assert.Equal(0, save.Count);
        var restore = Assert.IsType<RestoreCanvasCommand>(canvas.Commands![1]);
        Assert.Equal(0, restore.Count);
    }

    [Fact]
    public void DrawPicture_RecordsCommand()
    {
        var canvas = CreateCanvas();
        var picture = new SKPicture(
            SKRect.Create(0, 0, 5, 5),
            new List<CanvasCommand> { new SaveCanvasCommand(0) });

        canvas.DrawPicture(picture);

        var command = Assert.IsType<DrawPictureCanvasCommand>(canvas.Commands!.Single());
        Assert.Same(picture, command.Picture);
    }

    [Fact]
    public void PushCommandSource_AppliesMetadataAndRestoresNestedScopes()
    {
        var canvas = CreateCanvas();

        using (canvas.PushCommandSource("outer", "0", "SvgGroup"))
        {
            canvas.Save();

            using (canvas.PushCommandSource("inner", "0/1", "SvgPath"))
            {
                canvas.DrawPath(CloneTestData.CreatePath(), CloneTestData.CreatePaint());
            }

            canvas.Restore();
        }

        canvas.DrawPath(CloneTestData.CreatePath(), CloneTestData.CreatePaint());

        var save = Assert.IsType<SaveCanvasCommand>(canvas.Commands![0]);
        Assert.Equal("outer", save.SourceElementId);
        Assert.Equal("0", save.SourceElementAddress);
        Assert.Equal("SvgGroup", save.SourceElementTypeName);

        var inner = Assert.IsType<DrawPathCanvasCommand>(canvas.Commands![1]);
        Assert.Equal("inner", inner.SourceElementId);
        Assert.Equal("0/1", inner.SourceElementAddress);
        Assert.Equal("SvgPath", inner.SourceElementTypeName);

        var restore = Assert.IsType<RestoreCanvasCommand>(canvas.Commands![2]);
        Assert.Equal("outer", restore.SourceElementId);
        Assert.Equal("0", restore.SourceElementAddress);
        Assert.Equal("SvgGroup", restore.SourceElementTypeName);

        var unscoped = Assert.IsType<DrawPathCanvasCommand>(canvas.Commands![3]);
        Assert.Null(unscoped.SourceElementId);
        Assert.Null(unscoped.SourceElementAddress);
        Assert.Null(unscoped.SourceElementTypeName);
    }

    [Fact]
    public void DeepClone_DoesNotCarryActiveCommandSourceScopeToFutureCommands()
    {
        var canvas = CreateCanvas();

        using var _ = canvas.PushCommandSource("target", "0/1", "SvgPath");
        var clone = canvas.DeepClone();

        clone.DrawPath(CloneTestData.CreatePath(), CloneTestData.CreatePaint());

        var command = Assert.IsType<DrawPathCanvasCommand>(clone.Commands!.Single());
        Assert.Null(command.SourceElementId);
        Assert.Null(command.SourceElementAddress);
        Assert.Null(command.SourceElementTypeName);
    }
}
