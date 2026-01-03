using System.Collections.Generic;
using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class CloneCommandTests
{
    [Fact]
    public void PathCommand_DeepClone_CopiesValueCommands()
    {
        var commands = new PathCommand[]
        {
            new AddCirclePathCommand(1, 2, 3),
            new AddOvalPathCommand(SKRect.Create(1, 2, 3, 4)),
            new AddRectPathCommand(SKRect.Create(2, 3, 4, 5)),
            new AddRoundRectPathCommand(SKRect.Create(3, 4, 5, 6), 1, 2),
            new ArcToPathCommand(1, 2, 3, SKPathArcSize.Large, SKPathDirection.Clockwise, 4, 5),
            new ClosePathCommand(),
            new CubicToPathCommand(1, 2, 3, 4, 5, 6),
            new LineToPathCommand(7, 8),
            new MoveToPathCommand(9, 10),
            new QuadToPathCommand(11, 12, 13, 14)
        };

        foreach (var command in commands)
        {
            var clone = command.DeepClone();
            Assert.IsType(command.GetType(), clone);
            Assert.NotSame(command, clone);
            Assert.Equal(command, clone);
        }
    }

    [Fact]
    public void PathCommand_DeepClone_ClonesPolyPoints()
    {
        var points = new List<SKPoint> { new SKPoint(1, 2), new SKPoint(3, 4) };
        PathCommand command = new AddPolyPathCommand(points, true);

        var clone = command.DeepClone();
        var typed = Assert.IsType<AddPolyPathCommand>(clone);

        Assert.NotSame(points, typed.Points);
        Assert.Equal(points, typed.Points);
        Assert.True(typed.Close);
    }

    [Fact]
    public void CanvasCommand_DeepClone_CopiesValueCommands()
    {
        var commands = new CanvasCommand[]
        {
            new ClipRectCanvasCommand(SKRect.Create(1, 2, 3, 4), SKClipOperation.Difference, true),
            new RestoreCanvasCommand(2),
            new SaveCanvasCommand(3),
            new SetMatrixCanvasCommand(SKMatrix.CreateScale(2, 3), SKMatrix.CreateTranslation(4, 5))
        };

        foreach (var command in commands)
        {
            var clone = command.DeepClone();
            Assert.IsType(command.GetType(), clone);
            Assert.NotSame(command, clone);
            Assert.Equal(command, clone);
        }
    }

    [Fact]
    public void CanvasCommand_DeepClone_ClonesClipPath()
    {
        var clipPath = CloneTestData.CreateClipPath();
        CanvasCommand command = new ClipPathCanvasCommand(clipPath, SKClipOperation.Intersect, true);

        var clone = command.DeepClone();
        var typed = Assert.IsType<ClipPathCanvasCommand>(clone);

        Assert.Equal(SKClipOperation.Intersect, typed.Operation);
        Assert.True(typed.Antialias);
        Assert.NotSame(clipPath, typed.ClipPath);
        Assert.NotSame(clipPath.Clips, typed.ClipPath!.Clips);
    }

    [Fact]
    public void CanvasCommand_DeepClone_ClonesDrawImage()
    {
        var image = CloneTestData.CreateImage();
        var paint = CloneTestData.CreatePaint();
        CanvasCommand command = new DrawImageCanvasCommand(image, SKRect.Create(0, 0, 10, 10), SKRect.Create(1, 1, 5, 5), paint);

        var clone = command.DeepClone();
        var typed = Assert.IsType<DrawImageCanvasCommand>(clone);

        Assert.NotSame(image, typed.Image);
        Assert.NotSame(paint, typed.Paint);
        Assert.NotSame(image.Data, typed.Image!.Data);
        Assert.Equal(SKRect.Create(0, 0, 10, 10), typed.Source);
        Assert.Equal(SKRect.Create(1, 1, 5, 5), typed.Dest);
    }

    [Fact]
    public void CanvasCommand_DeepClone_ClonesDrawPath()
    {
        var path = CloneTestData.CreatePath();
        var paint = CloneTestData.CreatePaint();
        CanvasCommand command = new DrawPathCanvasCommand(path, paint);

        var clone = command.DeepClone();
        var typed = Assert.IsType<DrawPathCanvasCommand>(clone);

        Assert.NotSame(path, typed.Path);
        Assert.NotSame(paint, typed.Paint);
    }

    [Fact]
    public void CanvasCommand_DeepClone_ClonesDrawTextBlob()
    {
        var textBlob = CloneTestData.CreateTextBlob();
        var paint = CloneTestData.CreatePaint();
        CanvasCommand command = new DrawTextBlobCanvasCommand(textBlob, 1, 2, paint);

        var clone = command.DeepClone();
        var typed = Assert.IsType<DrawTextBlobCanvasCommand>(clone);

        Assert.NotSame(textBlob, typed.TextBlob);
        Assert.NotSame(paint, typed.Paint);
        Assert.NotSame(textBlob.Points, typed.TextBlob!.Points);
        Assert.Equal(1, typed.X);
        Assert.Equal(2, typed.Y);
    }

    [Fact]
    public void CanvasCommand_DeepClone_ClonesDrawText()
    {
        var paint = CloneTestData.CreatePaint();
        CanvasCommand command = new DrawTextCanvasCommand("Text", 1, 2, paint);

        var clone = command.DeepClone();
        var typed = Assert.IsType<DrawTextCanvasCommand>(clone);

        Assert.Equal("Text", typed.Text);
        Assert.Equal(1, typed.X);
        Assert.Equal(2, typed.Y);
        Assert.NotSame(paint, typed.Paint);
    }

    [Fact]
    public void CanvasCommand_DeepClone_ClonesDrawTextOnPath()
    {
        var path = CloneTestData.CreatePath();
        var paint = CloneTestData.CreatePaint();
        CanvasCommand command = new DrawTextOnPathCanvasCommand("Text", path, 1, 2, paint);

        var clone = command.DeepClone();
        var typed = Assert.IsType<DrawTextOnPathCanvasCommand>(clone);

        Assert.NotSame(path, typed.Path);
        Assert.NotSame(paint, typed.Paint);
        Assert.Equal("Text", typed.Text);
        Assert.Equal(1, typed.HOffset);
        Assert.Equal(2, typed.VOffset);
    }

    [Fact]
    public void CanvasCommand_DeepClone_ClonesSaveLayer()
    {
        var paint = CloneTestData.CreatePaint();
        CanvasCommand command = new SaveLayerCanvasCommand(1, paint);

        var clone = command.DeepClone();
        var typed = Assert.IsType<SaveLayerCanvasCommand>(clone);

        Assert.Equal(1, typed.Count);
        Assert.NotSame(paint, typed.Paint);
    }
}
