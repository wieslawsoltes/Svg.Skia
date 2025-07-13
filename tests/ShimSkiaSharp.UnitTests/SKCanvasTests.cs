using System.Linq;
using Xunit;
using ShimSkiaSharp;

namespace ShimSkiaSharp.UnitTests;

public class SKCanvasTests
{
    private static SKCanvas CreateCanvas()
    {
        var recorder = new SKPictureRecorder();
        return recorder.BeginRecording(SKRect.Create(0,0,10,10));
    }

    [Fact]
    public void SetMatrix_AddsCommandAndUpdatesMatrix()
    {
        var canvas = CreateCanvas();
        var delta = SKMatrix.CreateTranslation(5,5);
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
}
