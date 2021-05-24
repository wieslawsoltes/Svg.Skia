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

        public static SKBitmap? ToBitmap(this SKPicture skPicture, SKColor background, float scaleX, float scaleY, SKColorType skColorType, SKAlphaType skAlphaType, SKColorSpace skColorSpace)
        {
            var width = skPicture.CullRect.Width * scaleX;
            var height = skPicture.CullRect.Height * scaleY;
            if (!(width > 0) || !(height > 0))
            {
                return null;
            }
            var skImageInfo = new SKImageInfo((int)width, (int)height, skColorType, skAlphaType, skColorSpace);
            var skBitmap = new SKBitmap(skImageInfo);
            using var skCanvas = new SKCanvas(skBitmap);
            Draw(skPicture, background, scaleX, scaleY, skCanvas);
            return skBitmap;
        }

        public static bool ToImage(this SKPicture skPicture, Stream stream, SKColor background, SKEncodedImageFormat format, int quality, float scaleX, float scaleY, SKColorType skColorType, SKAlphaType skAlphaType, SKColorSpace skColorSpace)
        {
            using var skBitmap = skPicture.ToBitmap(background, scaleX, scaleY, skColorType, skAlphaType, skColorSpace);
            if (skBitmap is null)
            {
                return false;
            }
            using var skImage = SKImage.FromBitmap(skBitmap);
            using var skData = skImage.Encode(format, quality);
            if (skData is { })
            {
                skData.SaveTo(stream);
                return true;
            }
            return false;
        }

        public static bool ToSvg(this SKPicture skPicture, string path, SKColor background, float scaleX, float scaleY)
        {
            var width = skPicture.CullRect.Width * scaleX;
            var height = skPicture.CullRect.Height * scaleY;
            if (width <= 0 || height <= 0)
            {
                return false;
            }
            using var skFileWStream = new SKFileWStream(path);
            using var skCanvas = SKSvgCanvas.Create(SKRect.Create(0, 0, width, height), skFileWStream);
            Draw(skPicture, background, scaleX, scaleY, skCanvas);
            return true;
        }

        public static bool ToPdf(this SKPicture skPicture, string path, SKColor background, float scaleX, float scaleY)
        {
            var width = skPicture.CullRect.Width * scaleX;
            var height = skPicture.CullRect.Height * scaleY;
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
            var width = skPicture.CullRect.Width * scaleX;
            var height = skPicture.CullRect.Height * scaleY;
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
