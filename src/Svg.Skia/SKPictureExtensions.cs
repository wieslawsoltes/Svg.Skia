// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.IO;

namespace Svg.Skia;

public static class SKPictureExtensions
{
    public static bool TryGetImageInfo(this SkiaSharp.SKPicture skPicture, float scaleX, float scaleY, SkiaSharp.SKColorType skColorType, SkiaSharp.SKAlphaType skAlphaType, SkiaSharp.SKColorSpace? skColorSpace, out SkiaSharp.SKImageInfo skImageInfo)
    {
        if (!TryGetRasterDimensions(skPicture, scaleX, scaleY, out var width, out var height))
        {
            skImageInfo = default;
            return false;
        }

        skImageInfo = new SkiaSharp.SKImageInfo(width, height, skColorType, skAlphaType, skColorSpace);
        return true;
    }

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

    public static bool ToBitmap(this SkiaSharp.SKPicture skPicture, SkiaSharp.SKBitmap skBitmap, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        using var skCanvas = new SkiaSharp.SKCanvas(skBitmap);
        return ToBitmap(skPicture, skBitmap, skCanvas, background, scaleX, scaleY);
    }

    public static bool ToBitmap(this SkiaSharp.SKPicture skPicture, SkiaSharp.SKBitmap skBitmap, SkiaSharp.SKCanvas skCanvas, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        if (!TryMatchRasterTargetDimensions(skPicture, scaleX, scaleY, skBitmap.Width, skBitmap.Height))
        {
            return false;
        }

        Draw(skPicture, background, scaleX, scaleY, skCanvas);
        return true;
    }

    public static SkiaSharp.SKBitmap? ToBitmap(this SkiaSharp.SKPicture skPicture, SkiaSharp.SKColor background, float scaleX, float scaleY, SkiaSharp.SKColorType skColorType, SkiaSharp.SKAlphaType skAlphaType, SkiaSharp.SKColorSpace skColorSpace)
    {
        if (!TryGetImageInfo(skPicture, scaleX, scaleY, skColorType, skAlphaType, skColorSpace, out var skImageInfo))
        {
            return null;
        }

        var skBitmap = new SkiaSharp.SKBitmap(skImageInfo);
        using var skCanvas = new SkiaSharp.SKCanvas(skBitmap);
        Draw(skPicture, background, scaleX, scaleY, skCanvas);
        return skBitmap;
    }

    public static bool ToImage(this SkiaSharp.SKPicture skPicture, Stream stream, SkiaSharp.SKSurface skSurface, SkiaSharp.SKColor background, SkiaSharp.SKEncodedImageFormat format, int quality, float scaleX, float scaleY)
    {
        var deviceClipBounds = skSurface.Canvas.DeviceClipBounds;
        if (!TryMatchRasterTargetDimensions(skPicture, scaleX, scaleY, deviceClipBounds.Width, deviceClipBounds.Height))
        {
            return false;
        }

        Draw(skPicture, background, scaleX, scaleY, skSurface.Canvas);
        using var skPixmap = skSurface.PeekPixels();
        return skPixmap is not null && EncodePixmap(skPixmap, stream, format, quality);
    }

    public static bool ToImage(this SkiaSharp.SKPicture skPicture, Stream stream, SkiaSharp.SKColor background, SkiaSharp.SKEncodedImageFormat format, int quality, float scaleX, float scaleY, SkiaSharp.SKColorType skColorType, SkiaSharp.SKAlphaType skAlphaType, SkiaSharp.SKColorSpace skColorSpace)
    {
        if (!TryGetImageInfo(skPicture, scaleX, scaleY, skColorType, skAlphaType, skColorSpace, out var skImageInfo))
        {
            return false;
        }

        using var skBitmap = new SkiaSharp.SKBitmap(skImageInfo);
        using var skCanvas = new SkiaSharp.SKCanvas(skBitmap);
        if (!ToBitmap(skPicture, skBitmap, skCanvas, background, scaleX, scaleY))
        {
            return false;
        }

        using var skPixmap = skBitmap.PeekPixels();
        return skPixmap is not null && EncodePixmap(skPixmap, stream, format, quality);
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

    private static bool TryGetRasterDimensions(this SkiaSharp.SKPicture skPicture, float scaleX, float scaleY, out int width, out int height)
    {
        var scaledWidth = skPicture.CullRect.Width * scaleX;
        var scaledHeight = skPicture.CullRect.Height * scaleY;
        if (!(scaledWidth > 0) || !(scaledHeight > 0))
        {
            width = 0;
            height = 0;
            return false;
        }

        width = (int)scaledWidth;
        height = (int)scaledHeight;
        return true;
    }

    private static bool TryMatchRasterTargetDimensions(this SkiaSharp.SKPicture skPicture, float scaleX, float scaleY, int width, int height)
    {
        return TryGetRasterDimensions(skPicture, scaleX, scaleY, out var expectedWidth, out var expectedHeight) &&
               expectedWidth == width &&
               expectedHeight == height;
    }

    private static bool EncodePixmap(
        SkiaSharp.SKPixmap skPixmap,
        Stream stream,
        SkiaSharp.SKEncodedImageFormat format,
        int quality)
    {
        return format == SkiaSharp.SKEncodedImageFormat.Png
            ? skPixmap.Encode(
                stream,
                new SkiaSharp.SKPngEncoderOptions(
                    SkiaSharp.SKPngEncoderFilterFlags.None,
                    1))
            : skPixmap.Encode(stream, format, quality);
    }
}
