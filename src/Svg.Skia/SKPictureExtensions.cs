// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.IO;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SKPictureExtensions
    {
        public static void Draw(this SKPicture skPicture, SKColor background, float scaleX, float scaleY, SKCanvas skCanvas)
        {
            skCanvas.DrawColor(background);
            skCanvas.Save();
            skCanvas.Scale(scaleX, scaleY);
            skCanvas.DrawPicture(skPicture);
            skCanvas.Restore();
        }
#if USE_COLORSPACE
        public static SKBitmap? ToBitmap(this SKPicture skPicture, SKColor background, float scaleX, float scaleY, SKColorType skColorType, SKAlphaType skAlphaType, SKColorSpace skColorSpace)
#else
        public static SKBitmap? ToBitmap(this SKPicture skPicture, SKColor background, float scaleX, float scaleY, SKColorType skColorType, SKAlphaType skAlphaType)
#endif
        {
            float width = skPicture.CullRect.Width * scaleX;
            float height = skPicture.CullRect.Height * scaleY;
            if (width > 0 && height > 0)
            {
#if USE_COLORSPACE
                var skImageInfo = new SKImageInfo((int)width, (int)height, skColorType, skAlphaType, skColorSpace);
#else
                var skImageInfo = new SKImageInfo((int)width, (int)height, skColorType, skAlphaType);
#endif
                var skBitmap = new SKBitmap(skImageInfo);
                using var skCanvas = new SKCanvas(skBitmap);
                Draw(skPicture, background, scaleX, scaleY, skCanvas);
                return skBitmap;
            }
            return null;
        }

#if USE_COLORSPACE
        public static bool ToImage(this SKPicture skPicture, Stream stream, SKColor background, SKEncodedImageFormat format, int quality, float scaleX, float scaleY, SKColorType skColorType, SKAlphaType skAlphaType, SKColorSpace skColorSpace)
        {
            using (var skBitmap = skPicture.ToBitmap(background, scaleX, scaleY, skColorType, skAlphaType, skColorSpace))
            {
#else
        public static bool ToImage(this SKPicture skPicture, Stream stream, SKColor background, SKEncodedImageFormat format, int quality, float scaleX, float scaleY, SKColorType skColorType, SKAlphaType skAlphaType)
        {
            using (var skBitmap = skPicture.ToBitmap(background, scaleX, scaleY, skColorType, skAlphaType))
            {
#endif
                if (skBitmap == null)
                {
                    return false;
                }
                using var skImage = SKImage.FromBitmap(skBitmap);
                using var skData = skImage.Encode(format, quality);
                if (skData != null)
                {
                    skData.SaveTo(stream);
                    return true;
                }
            }
            return false;
        }

        public static bool ToSvg(this SKPicture skPicture, string path, SKColor background, float scaleX, float scaleY)
        {
            float width = skPicture.CullRect.Width * scaleX;
            float height = skPicture.CullRect.Height * scaleY;
            if (width <= 0 || height <= 0)
            {
                return false;
            }
            using var skFileWStream = new SKFileWStream(path);
            using var writer = new SKXmlStreamWriter(skFileWStream);
            using var skCanvas = SKSvgCanvas.Create(SKRect.Create(0, 0, width, height), writer);
            Draw(skPicture, background, scaleX, scaleY, skCanvas);
            return true;
        }

        public static bool ToPdf(this SKPicture skPicture, string path, SKColor background, float scaleX, float scaleY)
        {
            float width = skPicture.CullRect.Width * scaleX;
            float height = skPicture.CullRect.Height * scaleY;
            if (width <= 0 || height <= 0)
            {
                return false;
            }
            using var skFileWStream = new SKFileWStream(path);
            using var skDocument = SKDocument.CreatePdf(skFileWStream, SKDocument.DefaultRasterDpi);
            using var skCanvas = skDocument.BeginPage(width, height);
            Draw(skPicture, background, scaleX, scaleY, skCanvas);
            skDocument.Close();
            return true;
        }

        public static bool ToXps(this SKPicture skPicture, string path, SKColor background, float scaleX, float scaleY)
        {
            float width = skPicture.CullRect.Width * scaleX;
            float height = skPicture.CullRect.Height * scaleY;
            if (width <= 0 || height <= 0)
            {
                return false;
            }
            using var skFileWStream = new SKFileWStream(path);
            using var skDocument = SKDocument.CreateXps(skFileWStream, SKDocument.DefaultRasterDpi);
            using var skCanvas = skDocument.BeginPage(width, height);
            Draw(skPicture, background, scaleX, scaleY, skCanvas);
            skDocument.Close();
            return true;
        }
    }
}
