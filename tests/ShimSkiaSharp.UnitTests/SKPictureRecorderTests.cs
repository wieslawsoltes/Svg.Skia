using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class SKPictureRecorderTests
{
    [Fact]
    public void BeginAndEndRecording_Works()
    {
        var recorder = new SKPictureRecorder();
        var rect = SKRect.Create(0, 0, 10, 10);
        var canvas = recorder.BeginRecording(rect);
        Assert.NotNull(canvas);
        Assert.Equal(SKMatrix.Identity, canvas.TotalMatrix);
        canvas.Save();

        var picture = recorder.EndRecording();
        Assert.Equal(rect, picture.CullRect);
        Assert.NotNull(picture.Commands);
        Assert.Single(picture.Commands);
        Assert.IsType<SaveCanvasCommand>(picture.Commands![0]);
        Assert.Equal(SKRect.Empty, recorder.CullRect);
        Assert.Null(recorder.RecordingCanvas);
    }
}
