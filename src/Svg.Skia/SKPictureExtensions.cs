// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.IO;

namespace Svg.Skia;

public static class SKPictureExtensions
{
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

        using var skSurface = SkiaSharp.SKSurface.Create(skImageInfo);
        if (skSurface is null)
        {
            return false;
        }

        Draw(skPicture, background, scaleX, scaleY, skSurface.Canvas);
        using var skImage = skSurface.Snapshot();
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
}
