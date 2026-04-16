using System;
using System.Collections.Generic;
using System.Reflection;
using ShimSkiaSharp;
using SkiaSharp;
using Svg.Skia.TypefaceProviders;
using Xunit;
using NativeTextBlob = SkiaSharp.SKTextBlob;

namespace Svg.Skia.UnitTests;

public class SkiaModelReplayTests
{
    [Fact]
    public void Draw_OptimizedReplayMatchesPerCommandDispatch()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40" viewBox="0 0 40 40">
              <defs>
                <clipPath id="clip-a">
                  <rect x="2" y="2" width="30" height="30" />
                </clipPath>
              </defs>
              <g transform="translate(3,4)" opacity="0.7" clip-path="url(#clip-a)">
                <rect x="0" y="0" width="28" height="20" fill="#cc3344" />
                <g opacity="0.5">
                  <rect x="6" y="8" width="20" height="16" fill="#3366cc" />
                </g>
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        _ = svg.FromSvg(svgMarkup);
        var picture = svg.Model;

        Assert.NotNull(picture);

        using var optimizedPicture = svg.SkiaModel.ToSKPicture(picture);
        Assert.NotNull(optimizedPicture);

        using var recorder = new SkiaSharp.SKPictureRecorder();
        using var canvas = recorder.BeginRecording(optimizedPicture!.CullRect);
        var commands = picture!.Commands;
        Assert.NotNull(commands);

        for (var i = 0; i < commands!.Count; i++)
        {
            svg.SkiaModel.Draw(commands[i], canvas);
        }

        using var perCommandPicture = recorder.EndRecording();
        AssertPicturesEqual(optimizedPicture, perCommandPicture);
    }

    [Fact]
    public void PositionedTextBlobCache_ReusesAcrossFreshDefaultPathModels()
    {
        var command = GetPositionedTextBlobCommand();

        var firstModel = new SkiaModel(new SKSvgSettings());
        var secondModel = new SkiaModel(new SKSvgSettings());

        var firstBlob = GetCachedPositionedTextBlob(firstModel, command);
        var secondBlob = GetCachedPositionedTextBlob(secondModel, command);

        Assert.NotNull(firstBlob);
        Assert.Same(firstBlob, secondBlob);
    }

    [Fact]
    public void PositionedTextBlobCache_RemainsInstanceScopedForCustomProviderModels()
    {
        var command = GetPositionedTextBlobCommand();

        var firstModel = new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { new FontManagerTypefaceProvider() }
        });
        var secondModel = new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { new FontManagerTypefaceProvider() }
        });

        var firstBlob = GetCachedPositionedTextBlob(firstModel, command);
        var secondBlob = GetCachedPositionedTextBlob(secondModel, command);

        Assert.NotNull(firstBlob);
        Assert.NotNull(secondBlob);
        Assert.NotSame(firstBlob, secondBlob);
    }

    [Fact]
    public void RenderPathCache_ReusesAcrossFreshModelsForEquivalentPaths()
    {
        var firstModel = new SkiaModel(new SKSvgSettings());
        var secondModel = new SkiaModel(new SKSvgSettings());
        var firstPath = GetSamplePath();
        var secondPath = GetSamplePath();

        var firstNativePath = GetRenderPath(firstModel, firstPath);
        var secondNativePath = GetRenderPath(secondModel, secondPath);

        Assert.NotNull(firstNativePath);
        Assert.Same(firstNativePath, secondNativePath);
    }

    [Fact]
    public void RenderPathCache_InvalidatesWhenPathMutates()
    {
        var model = new SkiaModel(new SKSvgSettings());
        var path = GetSamplePath();

        var originalNativePath = GetRenderPath(model, path);
        path.LineTo(50f, 18f);
        var mutatedNativePath = GetRenderPath(model, path);

        Assert.NotNull(originalNativePath);
        Assert.NotNull(mutatedNativePath);
        Assert.NotSame(originalNativePath, mutatedNativePath);
    }

    private static void AssertPicturesEqual(SkiaSharp.SKPicture expected, SkiaSharp.SKPicture actual)
    {
        var left = MathF.Min(expected.CullRect.Left, actual.CullRect.Left);
        var top = MathF.Min(expected.CullRect.Top, actual.CullRect.Top);
        var right = MathF.Max(expected.CullRect.Right, actual.CullRect.Right);
        var bottom = MathF.Max(expected.CullRect.Bottom, actual.CullRect.Bottom);
        var width = Math.Max(1, (int)MathF.Ceiling(right - left));
        var height = Math.Max(1, (int)MathF.Ceiling(bottom - top));

        using var expectedBitmap = RenderPicture(expected, width, height, left, top);
        using var actualBitmap = RenderPicture(actual, width, height, left, top);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Assert.Equal(expectedBitmap.GetPixel(x, y), actualBitmap.GetPixel(x, y));
            }
        }
    }

    private static SKBitmap RenderPicture(SkiaSharp.SKPicture picture, int width, int height, float left, float top)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(-left, -top);
        canvas.DrawPicture(picture);
        return bitmap;
    }

    private static DrawTextBlobCanvasCommand GetPositionedTextBlobCommand()
    {
        var typeface = ShimSkiaSharp.SKTypeface.FromFamilyName(
            SkiaSharp.SKTypeface.Default.FamilyName,
            ShimSkiaSharp.SKFontStyleWeight.Normal,
            ShimSkiaSharp.SKFontStyleWidth.Normal,
            ShimSkiaSharp.SKFontStyleSlant.Upright);

        var paint = new ShimSkiaSharp.SKPaint
        {
            IsAntialias = true,
            TextSize = 16f,
            Typeface = typeface
        };

        var textBlob = ShimSkiaSharp.SKTextBlob.CreatePositioned(
            "Blob",
            new[]
            {
                new ShimSkiaSharp.SKPoint(10f, 20f),
                new ShimSkiaSharp.SKPoint(30f, 20f),
                new ShimSkiaSharp.SKPoint(50f, 20f),
                new ShimSkiaSharp.SKPoint(70f, 20f)
            });

        return new DrawTextBlobCanvasCommand(textBlob, 0f, 0f, paint);
    }

    private static ShimSkiaSharp.SKPath GetSamplePath()
    {
        var path = new ShimSkiaSharp.SKPath();
        path.MoveTo(2f, 3f);
        path.LineTo(14f, 6f);
        path.QuadTo(20f, 12f, 24f, 18f);
        path.CubicTo(28f, 22f, 34f, 26f, 38f, 30f);
        path.Close();
        return path;
    }

    private static NativeTextBlob? GetCachedPositionedTextBlob(SkiaModel model, DrawTextBlobCanvasCommand command)
    {
        using var paint = model.ToSKPaint(command.Paint);
        Assert.NotNull(paint);

        var method = typeof(SkiaModel).GetMethod(
            "GetCachedPositionedTextBlob",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        return method!.Invoke(model, new object?[] { command, paint! }) as NativeTextBlob;
    }

    private static SkiaSharp.SKPath? GetRenderPath(SkiaModel model, ShimSkiaSharp.SKPath path)
    {
        var method = typeof(SkiaModel).GetMethod(
            "GetRenderPath",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        return method!.Invoke(model, new object?[] { path }) as SkiaSharp.SKPath;
    }

}
