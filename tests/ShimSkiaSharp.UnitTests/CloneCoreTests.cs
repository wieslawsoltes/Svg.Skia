using System.Linq;
using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class CloneCoreTests
{
    [Fact]
    public void SKPaint_Clone_DeepClone_CopiesPropertiesAndNestedObjects()
    {
        var paint = CloneTestData.CreatePaint();

        AssertPaintClone(paint, paint.Clone());
        AssertPaintClone(paint, paint.DeepClone());
    }

    [Fact]
    public void SKPath_Clone_DeepClone_CopiesCommands()
    {
        var path = CloneTestData.CreatePath();

        AssertPathClone(path, path.Clone());
        AssertPathClone(path, path.DeepClone());
    }

    [Fact]
    public void SKImage_Clone_DeepClone_CopiesData()
    {
        var image = CloneTestData.CreateImage();

        AssertImageClone(image, image.Clone());
        AssertImageClone(image, image.DeepClone());
    }

    [Fact]
    public void SKTextBlob_Clone_DeepClone_CopiesPoints()
    {
        var textBlob = CloneTestData.CreateTextBlob();

        AssertTextBlobClone(textBlob, textBlob.Clone());
        AssertTextBlobClone(textBlob, textBlob.DeepClone());
    }

    [Fact]
    public void SKTypeface_Clone_DeepClone_CopiesProperties()
    {
        var typeface = SKTypeface.FromFamilyName("Test", SKFontStyleWeight.Bold, SKFontStyleWidth.Condensed, SKFontStyleSlant.Italic);

        AssertTypefaceClone(typeface, typeface.Clone());
        AssertTypefaceClone(typeface, typeface.DeepClone());
    }

    [Fact]
    public void ClipPath_Clone_DeepClone_CopiesNestedClips()
    {
        var clipPath = CloneTestData.CreateClipPath();

        AssertClipPathClone(clipPath, clipPath.Clone());
        AssertClipPathClone(clipPath, clipPath.DeepClone());
    }

    [Fact]
    public void PathClip_Clone_DeepClone_CopiesNestedPath()
    {
        var pathClip = new PathClip
        {
            Path = CloneTestData.CreatePath(),
            Transform = SKMatrix.CreateTranslation(2, 3),
            Clip = CloneTestData.CreateClipPath()
        };

        AssertPathClipClone(pathClip, pathClip.Clone());
        AssertPathClipClone(pathClip, pathClip.DeepClone());
    }

    [Fact]
    public void SKPicture_DeepClone_CopiesCommands()
    {
        var picture = CloneTestData.CreatePicture();

        var clone = picture.DeepClone();

        Assert.NotSame(picture, clone);
        Assert.Equal(picture.CullRect, clone.CullRect);
        Assert.NotSame(picture.Commands, clone.Commands);
        Assert.Equal(picture.Commands!.Count, clone.Commands!.Count);

        var originalCommand = Assert.IsType<DrawPathCanvasCommand>(picture.Commands![0]);
        var clonedCommand = Assert.IsType<DrawPathCanvasCommand>(clone.Commands![0]);
        Assert.NotSame(originalCommand.Path, clonedCommand.Path);
        Assert.NotSame(originalCommand.Paint, clonedCommand.Paint);
    }

    [Fact]
    public void SKCanvas_Clone_DeepClone_CopiesCommandsAndState()
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
        var paint = CloneTestData.CreatePaint();

        canvas.SetMatrix(SKMatrix.CreateTranslation(1, 2));
        canvas.Save();
        canvas.SetMatrix(SKMatrix.CreateScale(2, 3));
        canvas.DrawText("Clone", 1, 2, paint);

        AssertCanvasClone(canvas, canvas.Clone(), paint);
        AssertCanvasClone(canvas, canvas.DeepClone(), paint);
    }

    [Fact]
    public void SKCanvas_DeepClone_PreservesSharedPathAndPaint()
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
        var path = CloneTestData.CreatePath();
        var paint = CloneTestData.CreatePaint();

        canvas.DrawPath(path, paint);
        canvas.DrawPath(path, paint);

        var clone = canvas.DeepClone();

        var first = Assert.IsType<DrawPathCanvasCommand>(clone.Commands![0]);
        var second = Assert.IsType<DrawPathCanvasCommand>(clone.Commands![1]);

        Assert.Same(first.Path, second.Path);
        Assert.Same(first.Paint, second.Paint);
        Assert.NotSame(path, first.Path);
        Assert.NotSame(paint, first.Paint);
    }

    [Fact]
    public void SKCanvas_DeepClone_PreservesSharedShaderAcrossPaints()
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
        var shader = SKShader.CreateColor(new SKColor(1, 2, 3, 4), SKColorSpace.Srgb);
        var paintA = CloneTestData.CreatePaint();
        var paintB = CloneTestData.CreatePaint();
        paintA.Shader = shader;
        paintB.Shader = shader;

        canvas.DrawPath(CloneTestData.CreatePath(), paintA);
        canvas.DrawPath(CloneTestData.CreatePath(), paintB);

        var clone = canvas.DeepClone();

        var first = Assert.IsType<DrawPathCanvasCommand>(clone.Commands![0]);
        var second = Assert.IsType<DrawPathCanvasCommand>(clone.Commands![1]);

        Assert.NotSame(first.Paint, second.Paint);
        Assert.NotSame(shader, first.Paint!.Shader);
        Assert.Same(first.Paint.Shader, second.Paint!.Shader);
    }

    [Fact]
    public void SKPictureRecorder_Clone_DeepClone_CopiesRecordingCanvas()
    {
        var recorder = CloneTestData.CreateRecorderWithCommand();

        AssertRecorderClone(recorder, recorder.Clone());
        AssertRecorderClone(recorder, recorder.DeepClone());
    }

    private static void AssertPaintClone(SKPaint original, SKPaint clone)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.Style, clone.Style);
        Assert.Equal(original.IsAntialias, clone.IsAntialias);
        Assert.Equal(original.IsDither, clone.IsDither);
        Assert.Equal(original.StrokeWidth, clone.StrokeWidth);
        Assert.Equal(original.StrokeCap, clone.StrokeCap);
        Assert.Equal(original.StrokeJoin, clone.StrokeJoin);
        Assert.Equal(original.StrokeMiter, clone.StrokeMiter);
        Assert.Equal(original.TextSize, clone.TextSize);
        Assert.Equal(original.TextAlign, clone.TextAlign);
        Assert.Equal(original.LcdRenderText, clone.LcdRenderText);
        Assert.Equal(original.SubpixelText, clone.SubpixelText);
        Assert.Equal(original.TextEncoding, clone.TextEncoding);
        Assert.Equal(original.Color, clone.Color);
        Assert.Equal(original.BlendMode, clone.BlendMode);
        Assert.Equal(original.FilterQuality, clone.FilterQuality);

        Assert.NotNull(original.Typeface);
        Assert.NotNull(clone.Typeface);
        Assert.NotSame(original.Typeface, clone.Typeface);
        Assert.Equal(original.Typeface!.FamilyName, clone.Typeface!.FamilyName);
        Assert.Equal(original.Typeface.FontWeight, clone.Typeface.FontWeight);
        Assert.Equal(original.Typeface.FontWidth, clone.Typeface.FontWidth);
        Assert.Equal(original.Typeface.FontSlant, clone.Typeface.FontSlant);

        Assert.NotNull(original.Shader);
        Assert.NotNull(clone.Shader);
        Assert.NotSame(original.Shader, clone.Shader);

        Assert.NotNull(original.ColorFilter);
        Assert.NotNull(clone.ColorFilter);
        Assert.NotSame(original.ColorFilter, clone.ColorFilter);

        Assert.NotNull(original.ImageFilter);
        Assert.NotNull(clone.ImageFilter);
        Assert.NotSame(original.ImageFilter, clone.ImageFilter);

        Assert.NotNull(original.PathEffect);
        Assert.NotNull(clone.PathEffect);
        Assert.NotSame(original.PathEffect, clone.PathEffect);
    }

    private static void AssertPathClone(SKPath original, SKPath clone)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.FillType, clone.FillType);
        Assert.NotSame(original.Commands, clone.Commands);
        Assert.Equal(original.Commands!.Count, clone.Commands!.Count);

        var originalPoly = original.Commands.OfType<AddPolyPathCommand>().Single();
        var clonedPoly = clone.Commands.OfType<AddPolyPathCommand>().Single();
        Assert.NotSame(originalPoly.Points, clonedPoly.Points);
        Assert.Equal(originalPoly.Points, clonedPoly.Points);
    }

    private static void AssertImageClone(SKImage original, SKImage clone)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.Width, clone.Width);
        Assert.Equal(original.Height, clone.Height);
        Assert.NotSame(original.Data, clone.Data);
        Assert.Equal(original.Data, clone.Data);
    }

    private static void AssertTextBlobClone(SKTextBlob original, SKTextBlob clone)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.Text, clone.Text);
        Assert.NotSame(original.Points, clone.Points);
        Assert.Equal(original.Points, clone.Points);
    }

    private static void AssertTypefaceClone(SKTypeface original, SKTypeface clone)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.FamilyName, clone.FamilyName);
        Assert.Equal(original.FontWeight, clone.FontWeight);
        Assert.Equal(original.FontWidth, clone.FontWidth);
        Assert.Equal(original.FontSlant, clone.FontSlant);
    }

    private static void AssertClipPathClone(ClipPath original, ClipPath clone)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.Transform, clone.Transform);
        Assert.NotSame(original.Clips, clone.Clips);
        Assert.Equal(original.Clips!.Count, clone.Clips!.Count);
        Assert.NotSame(original.Clip, clone.Clip);
        Assert.Equal(original.Clip!.Clips!.Count, clone.Clip!.Clips!.Count);

        var originalClip = original.Clips[0];
        var clonedClip = clone.Clips[0];
        Assert.NotSame(originalClip, clonedClip);
        Assert.NotSame(originalClip.Path, clonedClip.Path);
        Assert.Equal(originalClip.Transform, clonedClip.Transform);
    }

    private static void AssertPathClipClone(PathClip original, PathClip clone)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.Transform, clone.Transform);
        Assert.NotSame(original.Path, clone.Path);
        Assert.NotSame(original.Clip, clone.Clip);
        Assert.NotSame(original.Path!.Commands, clone.Path!.Commands);
    }

    private static void AssertCanvasClone(SKCanvas original, SKCanvas clone, SKPaint originalPaint)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.TotalMatrix, clone.TotalMatrix);
        Assert.NotSame(original.Commands, clone.Commands);
        Assert.Equal(original.Commands!.Count, clone.Commands!.Count);

        var originalDrawText = Assert.IsType<DrawTextCanvasCommand>(original.Commands.Last());
        var clonedDrawText = Assert.IsType<DrawTextCanvasCommand>(clone.Commands.Last());
        Assert.NotSame(originalDrawText.Paint, clonedDrawText.Paint);
        Assert.NotSame(originalPaint, clonedDrawText.Paint);
        Assert.Equal(originalDrawText.Text, clonedDrawText.Text);

        clone.Restore();
        Assert.Equal(SKMatrix.CreateTranslation(1, 2), clone.TotalMatrix);
    }

    private static void AssertRecorderClone(SKPictureRecorder original, SKPictureRecorder clone)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.CullRect, clone.CullRect);
        Assert.NotSame(original.RecordingCanvas, clone.RecordingCanvas);
        Assert.NotSame(original.RecordingCanvas!.Commands, clone.RecordingCanvas!.Commands);
        Assert.Equal(original.RecordingCanvas.Commands!.Count, clone.RecordingCanvas.Commands!.Count);
    }
}
