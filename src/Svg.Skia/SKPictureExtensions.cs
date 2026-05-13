// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.IO;

namespace Svg.Skia;

public static class SKPictureExtensions
{
    private const float DownsampleRasterOversample = 4f;
    private const long MaxDownsampleRasterPixels = 16L * 1024L * 1024L;

    public static void Draw(this SkiaSharp.SKPicture skPicture, SkiaSharp.SKColor background, float scaleX, float scaleY, SkiaSharp.SKCanvas skCanvas)
    {
        skCanvas.Clear(background);
        if (scaleX == 1f && scaleY == 1f)
        {
            skCanvas.DrawPicture(skPicture);
            return;
        }

        skCanvas.Save();
        skCanvas.Scale(scaleX, scaleY);
        skCanvas.DrawPicture(skPicture);
        skCanvas.Restore();
    }

    public static SkiaSharp.SKBitmap? ToBitmap(this SkiaSharp.SKPicture skPicture, SkiaSharp.SKColor background, float scaleX, float scaleY, SkiaSharp.SKColorType skColorType, SkiaSharp.SKAlphaType skAlphaType, SkiaSharp.SKColorSpace skColorSpace)
    {
        if (!TryCreateImageInfo(skPicture, scaleX, scaleY, skColorType, skAlphaType, skColorSpace, out var skImageInfo))
        {
            return null;
        }

        GetRasterRenderScales(skPicture, skImageInfo, scaleX, scaleY, out var renderScaleX, out var renderScaleY);
        if (ShouldDownsample(scaleX, scaleY, renderScaleX, renderScaleY))
        {
            if (!TryCreateImageInfo(skPicture, renderScaleX, renderScaleY, skColorType, skAlphaType, skColorSpace, out var renderImageInfo))
            {
                return null;
            }

            using var renderSurface = SkiaSharp.SKSurface.Create(renderImageInfo);
            if (renderSurface is null)
            {
                return null;
            }

            Draw(skPicture, background, renderScaleX, renderScaleY, renderSurface.Canvas);
            using var renderImage = renderSurface.Snapshot();

            var downsampledBitmap = new SkiaSharp.SKBitmap(skImageInfo);
            using var downsampledCanvas = new SkiaSharp.SKCanvas(downsampledBitmap);
            Downsample(renderImage, skImageInfo, downsampledCanvas);
            return downsampledBitmap;
        }

        var skBitmap = new SkiaSharp.SKBitmap(skImageInfo);
        using var skCanvas = new SkiaSharp.SKCanvas(skBitmap);
        Draw(skPicture, background, scaleX, scaleY, skCanvas);
        return skBitmap;
    }

    public static bool ToImage(this SkiaSharp.SKPicture skPicture, Stream stream, SkiaSharp.SKColor background, SkiaSharp.SKEncodedImageFormat format, int quality, float scaleX, float scaleY, SkiaSharp.SKColorType skColorType, SkiaSharp.SKAlphaType skAlphaType, SkiaSharp.SKColorSpace skColorSpace)
    {
        if (!TryCreateImageInfo(skPicture, scaleX, scaleY, skColorType, skAlphaType, skColorSpace, out var skImageInfo))
        {
            return false;
        }

        GetRasterRenderScales(skPicture, skImageInfo, scaleX, scaleY, out var renderScaleX, out var renderScaleY);
        if (ShouldDownsample(scaleX, scaleY, renderScaleX, renderScaleY))
        {
            if (!TryCreateImageInfo(skPicture, renderScaleX, renderScaleY, skColorType, skAlphaType, skColorSpace, out var renderImageInfo))
            {
                return false;
            }

            using var renderSurface = SkiaSharp.SKSurface.Create(renderImageInfo);
            if (renderSurface is null)
            {
                return false;
            }

            Draw(skPicture, background, renderScaleX, renderScaleY, renderSurface.Canvas);
            using var renderImage = renderSurface.Snapshot();

            using var downsampledSurface = SkiaSharp.SKSurface.Create(skImageInfo);
            if (downsampledSurface is null)
            {
                return false;
            }

            Downsample(renderImage, skImageInfo, downsampledSurface.Canvas);
            using var downsampledImage = downsampledSurface.Snapshot();
            return Encode(downsampledImage, stream, format, quality);
        }

        using var skSurface = SkiaSharp.SKSurface.Create(skImageInfo);
        if (skSurface is null)
        {
            return false;
        }

        Draw(skPicture, background, scaleX, scaleY, skSurface.Canvas);
        using var skImage = skSurface.Snapshot();
        return Encode(skImage, stream, format, quality);
    }

    private static bool Encode(SkiaSharp.SKImage skImage, Stream stream, SkiaSharp.SKEncodedImageFormat format, int quality)
    {
        using var skData = skImage.Encode(format, quality);
        if (skData is { })
        {
            skData.SaveTo(stream);
            return true;
        }
        return false;
    }

    public static bool ToSvg(this SkiaSharp.SKPicture skPicture, string path, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        var width = skPicture.CullRect.Width * scaleX;
        var height = skPicture.CullRect.Height * scaleY;
        if (width <= 0 || height <= 0)
        {
            return false;
        }
        using var skFileWStream = new SkiaSharp.SKFileWStream(path);
        using var skCanvas = SkiaSharp.SKSvgCanvas.Create(SkiaSharp.SKRect.Create(0, 0, width, height), skFileWStream);
        Draw(skPicture, background, scaleX, scaleY, skCanvas);
        return true;
    }

    public static bool ToSvg(this SkiaSharp.SKPicture skPicture, Stream stream, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        var width = skPicture.CullRect.Width * scaleX;
        var height = skPicture.CullRect.Height * scaleY;
        if (width <= 0 || height <= 0)
        {
            return false;
        }
        using var skCanvas = SkiaSharp.SKSvgCanvas.Create(SkiaSharp.SKRect.Create(0, 0, width, height), stream);
        Draw(skPicture, background, scaleX, scaleY, skCanvas);
        return true;
    }

    public static bool ToPdf(this SkiaSharp.SKPicture skPicture, string path, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        var width = skPicture.CullRect.Width * scaleX;
        var height = skPicture.CullRect.Height * scaleY;
        if (width <= 0 || height <= 0)
        {
            return false;
        }
        using var skFileWStream = new SkiaSharp.SKFileWStream(path);
        using var skDocument = SkiaSharp.SKDocument.CreatePdf(skFileWStream, SkiaSharp.SKDocument.DefaultRasterDpi);
        using var skCanvas = skDocument.BeginPage(width, height);
        Draw(skPicture, background, scaleX, scaleY, skCanvas);
        skDocument.Close();
        return true;
    }

    public static bool ToPdf(this SkiaSharp.SKPicture skPicture, Stream stream, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        var width = skPicture.CullRect.Width * scaleX;
        var height = skPicture.CullRect.Height * scaleY;
        if (width <= 0 || height <= 0)
        {
            return false;
        }
        using var skDocument = SkiaSharp.SKDocument.CreatePdf(stream, SkiaSharp.SKDocument.DefaultRasterDpi);
        using var skCanvas = skDocument.BeginPage(width, height);
        Draw(skPicture, background, scaleX, scaleY, skCanvas);
        skDocument.Close();
        return true;
    }

    public static bool ToXps(this SkiaSharp.SKPicture skPicture, string path, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        var width = skPicture.CullRect.Width * scaleX;
        var height = skPicture.CullRect.Height * scaleY;
        if (width <= 0 || height <= 0)
        {
            return false;
        }
        using var skFileWStream = new SkiaSharp.SKFileWStream(path);
        using var skDocument = SkiaSharp.SKDocument.CreateXps(skFileWStream, SkiaSharp.SKDocument.DefaultRasterDpi);
        using var skCanvas = skDocument.BeginPage(width, height);
        Draw(skPicture, background, scaleX, scaleY, skCanvas);
        skDocument.Close();
        return true;
    }

    public static bool ToXps(this SkiaSharp.SKPicture skPicture, Stream stream, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        var width = skPicture.CullRect.Width * scaleX;
        var height = skPicture.CullRect.Height * scaleY;
        if (width <= 0 || height <= 0)
        {
            return false;
        }
        using var skDocument = SkiaSharp.SKDocument.CreateXps(stream, SkiaSharp.SKDocument.DefaultRasterDpi);
        using var skCanvas = skDocument.BeginPage(width, height);
        Draw(skPicture, background, scaleX, scaleY, skCanvas);
        skDocument.Close();
        return true;
    }

    private static bool TryCreateImageInfo(this SkiaSharp.SKPicture skPicture, float scaleX, float scaleY, SkiaSharp.SKColorType skColorType, SkiaSharp.SKAlphaType skAlphaType, SkiaSharp.SKColorSpace skColorSpace, out SkiaSharp.SKImageInfo skImageInfo)
    {
        var width = skPicture.CullRect.Width * scaleX;
        var height = skPicture.CullRect.Height * scaleY;
        if (!(width > 0) || !(height > 0))
        {
            skImageInfo = default;
            return false;
        }

        skImageInfo = new SkiaSharp.SKImageInfo((int)width, (int)height, skColorType, skAlphaType, skColorSpace);
        return true;
    }

    private static void GetRasterRenderScales(SkiaSharp.SKPicture skPicture, SkiaSharp.SKImageInfo targetImageInfo, float scaleX, float scaleY, out float renderScaleX, out float renderScaleY)
    {
        renderScaleX = GetRasterRenderScale(scaleX, float.PositiveInfinity);
        renderScaleY = GetRasterRenderScale(scaleY, float.PositiveInfinity);
        if (!ShouldDownsample(scaleX, scaleY, renderScaleX, renderScaleY) || FitsRasterBudget(skPicture, renderScaleX, renderScaleY))
        {
            return;
        }

        renderScaleX = GetRasterRenderScale(scaleX, DownsampleRasterOversample);
        renderScaleY = GetRasterRenderScale(scaleY, DownsampleRasterOversample);
        if (FitsRasterBudget(skPicture, renderScaleX, renderScaleY))
        {
            return;
        }

        var targetPixels = (long)targetImageInfo.Width * targetImageInfo.Height;
        if (targetPixels <= 0L || targetPixels >= MaxDownsampleRasterPixels)
        {
            renderScaleX = scaleX;
            renderScaleY = scaleY;
            return;
        }

        var boundedOversample = (float)System.Math.Sqrt((double)MaxDownsampleRasterPixels / targetPixels);
        if (boundedOversample < 1f)
        {
            boundedOversample = 1f;
        }
        else if (boundedOversample > DownsampleRasterOversample)
        {
            boundedOversample = DownsampleRasterOversample;
        }

        renderScaleX = GetRasterRenderScale(scaleX, boundedOversample);
        renderScaleY = GetRasterRenderScale(scaleY, boundedOversample);
    }

    private static float GetRasterRenderScale(float scale, float oversample)
    {
        if (scale >= 1f)
        {
            return scale;
        }

        var oversampledScale = scale * oversample;
        return oversampledScale < 1f ? oversampledScale : 1f;
    }

    private static bool FitsRasterBudget(SkiaSharp.SKPicture skPicture, float scaleX, float scaleY)
    {
        var width = skPicture.CullRect.Width * scaleX;
        var height = skPicture.CullRect.Height * scaleY;
        if (!(width > 0) || !(height > 0))
        {
            return false;
        }

        return (double)width * height <= MaxDownsampleRasterPixels;
    }

    private static bool ShouldDownsample(float scaleX, float scaleY, float renderScaleX, float renderScaleY)
    {
        return renderScaleX != scaleX || renderScaleY != scaleY;
    }

    private static void Downsample(SkiaSharp.SKImage renderImage, SkiaSharp.SKImageInfo targetImageInfo, SkiaSharp.SKCanvas targetCanvas)
    {
        using var paint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            BlendMode = SkiaSharp.SKBlendMode.Src
        };
        targetCanvas.DrawImage(
            renderImage,
            SkiaSharp.SKRect.Create(0f, 0f, targetImageInfo.Width, targetImageInfo.Height),
            new SkiaSharp.SKSamplingOptions(SkiaSharp.SKCubicResampler.Mitchell),
            paint);
    }
}
