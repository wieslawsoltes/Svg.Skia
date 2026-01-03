using System.Collections.Generic;
using ShimSkiaSharp;

namespace ShimSkiaSharp.UnitTests;

internal static class CloneTestData
{
    public static SKPaint CreatePaint()
    {
        return new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            IsDither = true,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Bevel,
            StrokeMiter = 3,
            Typeface = SKTypeface.FromFamilyName("Test", SKFontStyleWeight.Bold, SKFontStyleWidth.Condensed, SKFontStyleSlant.Italic),
            TextSize = 14,
            TextAlign = SKTextAlign.Center,
            LcdRenderText = true,
            SubpixelText = true,
            TextEncoding = SKTextEncoding.Utf16,
            Color = new SKColor(1, 2, 3, 4),
            Shader = SKShader.CreateColor(new SKColor(5, 6, 7, 8), SKColorSpace.Srgb),
            ColorFilter = SKColorFilter.CreateBlendMode(new SKColor(9, 10, 11, 12), SKBlendMode.Src),
            ImageFilter = SKImageFilter.CreateBlur(1f, 2f),
            PathEffect = SKPathEffect.CreateDash(new float[] { 1f, 2f, 3f }, 0.5f),
            BlendMode = SKBlendMode.Multiply,
            FilterQuality = SKFilterQuality.High
        };
    }

    public static SKPath CreatePath()
    {
        var path = new SKPath { FillType = SKPathFillType.EvenOdd };
        path.MoveTo(1, 2);
        path.LineTo(3, 4);
        path.AddRect(SKRect.Create(0, 0, 10, 20));
        path.AddPoly(new[] { new SKPoint(1, 1), new SKPoint(2, 2) }, true);
        return path;
    }

    public static SKImage CreateImage()
        => new SKImage { Data = new byte[] { 1, 2, 3, 4 }, Width = 10, Height = 20 };

    public static SKTextBlob CreateTextBlob()
        => SKTextBlob.CreatePositioned("Text", new[] { new SKPoint(1, 2), new SKPoint(3, 4) });

    public static ClipPath CreateClipPath()
    {
        var clip = new ClipPath
        {
            Transform = SKMatrix.CreateScale(2, 3),
            Clip = new ClipPath()
        };

        clip.Clips!.Add(new PathClip
        {
            Path = CreatePath(),
            Transform = SKMatrix.CreateTranslation(1, 2),
            Clip = clip.Clip
        });

        clip.Clip!.Clips!.Add(new PathClip
        {
            Path = CreatePath()
        });

        return clip;
    }

    public static SKPicture CreatePicture()
    {
        var commands = new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(CreatePath(), CreatePaint())
        };

        return new SKPicture(SKRect.Create(0, 0, 10, 10), commands);
    }

    public static LinearGradientShader CreateLinearGradientShader()
    {
        return (LinearGradientShader)SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(1, 1),
            new[] { new SKColorF(1f, 0f, 0f, 1f), new SKColorF(0f, 1f, 0f, 1f) },
            SKColorSpace.Srgb,
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp,
            SKMatrix.CreateScale(1, 2));
    }

    public static SKColorFilter CreateColorMatrixFilter()
        => SKColorFilter.CreateColorMatrix(new float[] { 1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f });

    public static SKImageFilter CreateLeafImageFilter()
        => SKImageFilter.CreateBlur(1f, 2f);

    public static SKPictureRecorder CreateRecorderWithCommand()
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0, 0, 10, 10));
        canvas.DrawText("Draw", 1, 2, CreatePaint());
        return recorder;
    }
}
