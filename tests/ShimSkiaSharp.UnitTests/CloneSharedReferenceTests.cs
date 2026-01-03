using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class CloneSharedReferenceTests
{
    [Fact]
    public void SKPath_DeepClone_PreservesSharedCommandsAndPoints()
    {
        var path = new SKPath();
        var points = new List<SKPoint> { new SKPoint(1, 2), new SKPoint(3, 4) };
        var sharedRect = new AddRectPathCommand(SKRect.Create(0, 0, 1, 1));

        path.Commands!.Add(new AddPolyPathCommand(points, true));
        path.Commands.Add(new AddPolyPathCommand(points, false));
        path.Commands.Add(sharedRect);
        path.Commands.Add(sharedRect);

        var clone = path.DeepClone();

        var polyA = Assert.IsType<AddPolyPathCommand>(clone.Commands![0]);
        var polyB = Assert.IsType<AddPolyPathCommand>(clone.Commands![1]);
        var rectA = Assert.IsType<AddRectPathCommand>(clone.Commands![2]);
        var rectB = Assert.IsType<AddRectPathCommand>(clone.Commands![3]);

        Assert.Same(polyA.Points, polyB.Points);
        Assert.NotSame(points, polyA.Points);
        Assert.Same(rectA, rectB);
        Assert.NotSame(sharedRect, rectA);
    }

    [Fact]
    public void ClipPath_DeepClone_PreservesSharedClip()
    {
        var clip = CloneTestData.CreateClipPath();

        var clone = clip.DeepClone();

        var clonedPathClip = Assert.IsType<PathClip>(clone.Clips![0]);
        Assert.NotSame(clip.Clip, clone.Clip);
        Assert.Same(clone.Clip, clonedPathClip.Clip);
    }

    [Fact]
    public void ClipPath_DeepClone_PreservesSharedPathReferences()
    {
        var sharedPath = CloneTestData.CreatePath();
        var clip = new ClipPath();
        clip.Clips!.Add(new PathClip { Path = sharedPath });
        clip.Clips.Add(new PathClip { Path = sharedPath });

        var clone = clip.DeepClone();

        var first = Assert.IsType<PathClip>(clone.Clips![0]);
        var second = Assert.IsType<PathClip>(clone.Clips![1]);

        Assert.Same(first.Path, second.Path);
        Assert.NotSame(sharedPath, first.Path);
    }

    [Fact]
    public void SKPicture_DeepClone_PreservesSharedCommandResources()
    {
        var sharedPath = CloneTestData.CreatePath();
        var sharedPaint = CloneTestData.CreatePaint();
        var commands = new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(sharedPath, sharedPaint),
            new DrawPathCanvasCommand(sharedPath, sharedPaint)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);

        var clone = picture.DeepClone();

        var first = Assert.IsType<DrawPathCanvasCommand>(clone.Commands![0]);
        var second = Assert.IsType<DrawPathCanvasCommand>(clone.Commands![1]);

        Assert.Same(first.Path, second.Path);
        Assert.Same(first.Paint, second.Paint);
        Assert.NotSame(sharedPath, first.Path);
        Assert.NotSame(sharedPaint, first.Paint);
    }

    [Fact]
    public void SKCanvas_DeepClone_PreservesSharedImageAndTextBlob()
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
        var sharedImage = CloneTestData.CreateImage();
        var sharedTextBlob = CloneTestData.CreateTextBlob();
        var sharedPaint = CloneTestData.CreatePaint();

        canvas.DrawImage(sharedImage, SKRect.Create(0, 0, 10, 10), SKRect.Create(1, 1, 5, 5), sharedPaint);
        canvas.DrawImage(sharedImage, SKRect.Create(0, 0, 10, 10), SKRect.Create(2, 2, 4, 4), sharedPaint);
        canvas.DrawText(sharedTextBlob, 1, 2, sharedPaint);
        canvas.DrawText(sharedTextBlob, 3, 4, sharedPaint);

        var clone = canvas.DeepClone();
        var imageCommands = clone.Commands!.OfType<DrawImageCanvasCommand>().ToList();
        var textCommands = clone.Commands!.OfType<DrawTextBlobCanvasCommand>().ToList();

        Assert.Same(imageCommands[0].Image, imageCommands[1].Image);
        Assert.Same(imageCommands[0].Paint, imageCommands[1].Paint);
        Assert.Same(textCommands[0].TextBlob, textCommands[1].TextBlob);
        Assert.Same(textCommands[0].Paint, textCommands[1].Paint);
        Assert.Same(imageCommands[0].Paint, textCommands[0].Paint);
        Assert.NotSame(sharedImage, imageCommands[0].Image);
        Assert.NotSame(sharedTextBlob, textCommands[0].TextBlob);
    }

    [Fact]
    public void SKCanvas_DeepClone_PreservesSharedPaintDependencies()
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
        var sharedTypeface = SKTypeface.FromFamilyName("Shared", SKFontStyleWeight.Bold, SKFontStyleWidth.Condensed, SKFontStyleSlant.Italic);
        var sharedShader = SKShader.CreateColor(new SKColor(1, 2, 3, 4), SKColorSpace.Srgb);
        var sharedColorFilter = SKColorFilter.CreateBlendMode(new SKColor(5, 6, 7, 8), SKBlendMode.Src);
        var sharedImageFilter = SKImageFilter.CreateBlur(1f, 2f);
        var sharedPathEffect = SKPathEffect.CreateDash(new float[] { 1f, 2f }, 0.5f);

        var paintA = new SKPaint
        {
            Typeface = sharedTypeface,
            Shader = sharedShader,
            ColorFilter = sharedColorFilter,
            ImageFilter = sharedImageFilter,
            PathEffect = sharedPathEffect
        };
        var paintB = new SKPaint
        {
            Typeface = sharedTypeface,
            Shader = sharedShader,
            ColorFilter = sharedColorFilter,
            ImageFilter = sharedImageFilter,
            PathEffect = sharedPathEffect
        };

        canvas.DrawPath(CloneTestData.CreatePath(), paintA);
        canvas.DrawPath(CloneTestData.CreatePath(), paintB);

        var clone = canvas.DeepClone();
        var first = Assert.IsType<DrawPathCanvasCommand>(clone.Commands![0]).Paint!;
        var second = Assert.IsType<DrawPathCanvasCommand>(clone.Commands![1]).Paint!;

        Assert.NotSame(first, second);
        Assert.Same(first.Typeface, second.Typeface);
        Assert.Same(first.Shader, second.Shader);
        Assert.Same(first.ColorFilter, second.ColorFilter);
        Assert.Same(first.ImageFilter, second.ImageFilter);
        Assert.Same(first.PathEffect, second.PathEffect);
        Assert.NotSame(sharedTypeface, first.Typeface);
        Assert.NotSame(sharedShader, first.Shader);
        Assert.NotSame(sharedColorFilter, first.ColorFilter);
        Assert.NotSame(sharedImageFilter, first.ImageFilter);
        Assert.NotSame(sharedPathEffect, first.PathEffect);
    }

    [Fact]
    public void SKImageFilter_DeepClone_PreservesSharedFiltersAndImages()
    {
        var sharedImage = CloneTestData.CreateImage();
        var filterA = SKImageFilter.CreateImage(sharedImage, SKRect.Create(0, 0, 10, 10), SKRect.Create(0, 0, 10, 10), SKFilterQuality.High);
        var filterB = SKImageFilter.CreateImage(sharedImage, SKRect.Create(1, 1, 8, 8), SKRect.Create(1, 1, 8, 8), SKFilterQuality.Low);
        var merge = SKImageFilter.CreateMerge(new[] { filterA, filterB, filterA });

        var clone = merge.DeepClone();
        var mergeClone = Assert.IsType<MergeImageFilter>(clone);
        var filters = mergeClone.Filters!;

        Assert.Equal(3, filters.Length);
        Assert.Same(filters[0], filters[2]);
        Assert.NotSame(filters[0], filters[1]);

        var imageFilterA = Assert.IsType<ImageImageFilter>(filters[0]);
        var imageFilterB = Assert.IsType<ImageImageFilter>(filters[1]);
        Assert.Same(imageFilterA.Image, imageFilterB.Image);
        Assert.NotSame(sharedImage, imageFilterA.Image);
    }

    [Fact]
    public void SKShader_DeepClone_PreservesSharedPicture()
    {
        var sharedPicture = CloneTestData.CreatePicture();
        var shaderA = SKShader.CreatePicture(sharedPicture, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKMatrix.Identity, SKRect.Create(0, 0, 10, 10));
        var shaderB = SKShader.CreatePicture(sharedPicture, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, SKMatrix.Identity, SKRect.Create(1, 1, 9, 9));

        var paintA = new SKPaint { Shader = shaderA };
        var paintB = new SKPaint { Shader = shaderB };

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
        canvas.DrawPath(CloneTestData.CreatePath(), paintA);
        canvas.DrawPath(CloneTestData.CreatePath(), paintB);

        var clone = canvas.DeepClone();
        var shaderCloneA = Assert.IsType<PictureShader>(Assert.IsType<DrawPathCanvasCommand>(clone.Commands![0]).Paint!.Shader);
        var shaderCloneB = Assert.IsType<PictureShader>(Assert.IsType<DrawPathCanvasCommand>(clone.Commands![1]).Paint!.Shader);

        Assert.NotSame(shaderCloneA, shaderCloneB);
        Assert.Same(shaderCloneA.Src, shaderCloneB.Src);
        Assert.NotSame(sharedPicture, shaderCloneA.Src);
    }

    [Fact]
    public void SKPictureRecorder_DeepClone_PreservesSharedCanvasResources()
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
        var sharedPath = CloneTestData.CreatePath();
        var sharedPaint = CloneTestData.CreatePaint();

        canvas.DrawPath(sharedPath, sharedPaint);
        canvas.DrawPath(sharedPath, sharedPaint);

        var clone = recorder.DeepClone();
        var first = Assert.IsType<DrawPathCanvasCommand>(clone.RecordingCanvas!.Commands![0]);
        var second = Assert.IsType<DrawPathCanvasCommand>(clone.RecordingCanvas!.Commands![1]);

        Assert.Same(first.Path, second.Path);
        Assert.Same(first.Paint, second.Paint);
        Assert.NotSame(sharedPath, first.Path);
        Assert.NotSame(sharedPaint, first.Paint);
    }

    [Fact]
    public void SharedCommandsList_DeepClone_PreservesListIdentityAcrossCanvasAndPicture()
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
        canvas.DrawPath(CloneTestData.CreatePath(), CloneTestData.CreatePaint());

        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), canvas.Commands);

        var context = CreateCloneContext();
        var canvasClone = DeepCloneWithContext(canvas, context);
        var pictureClone = DeepCloneWithContext(picture, context);

        Assert.Same(canvasClone.Commands, pictureClone.Commands);
        Assert.NotSame(canvas.Commands, canvasClone.Commands);
    }

    private static object CreateCloneContext()
    {
        var type = typeof(SKPaint).Assembly.GetType("ShimSkiaSharp.CloneContext", throwOnError: true);
        Assert.NotNull(type);
        return Activator.CreateInstance(type!, nonPublic: true)!;
    }

    private static T DeepCloneWithContext<T>(T instance, object context)
    {
        var method = instance!.GetType().GetMethod(
            "DeepClone",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { context.GetType() },
            modifiers: null);

        Assert.NotNull(method);
        return (T)method!.Invoke(instance, new[] { context })!;
    }
}
