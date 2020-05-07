using System;
using System.IO;
using System.IO.Compression;
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
            using var skCanvas = SKSvgCanvas.Create(SKRect.Create(0, 0, width, height), skFileWStream);
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

    public class SKSvg : IDisposable
    {
        public static SKAlphaType s_alphaType = SKAlphaType.Unpremul; // SKAlphaType.Unpremul, SKAlphaType.Premul

        public static SKColorType s_colorType = SKImageInfo.PlatformColorType; // SKImageInfo.PlatformColorType, SKColorType.RgbaF16

        static SKSvg()
        {
            SvgDocument.SkipGdiPlusCapabilityCheck = true;
        }

        public static void Draw(SKCanvas skCanvas, SvgFragment svgFragment)
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            var skBounds = SKRect.Create(skSize);
            using var drawable = DrawableFactory.Create(svgFragment, skBounds, null, Attributes.None);
            drawable?.PostProcess();
            drawable?.Draw(skCanvas, 0f, 0f);
        }

        public static void Draw(SKCanvas skCanvas, string path)
        {
            var svgDocument = Open(path);
            if (svgDocument != null)
            {
                Draw(skCanvas, svgDocument);
            }
        }

        public static SKPicture? ToPicture(SvgFragment svgFragment)
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            var skBounds = SKRect.Create(skSize);
            using var drawable = DrawableFactory.Create(svgFragment, skBounds, null, Attributes.None);
            if (drawable == null)
            {
                return null;
            }
            drawable.PostProcess();

            if (skBounds.IsEmpty)
            {
                var bounds = drawable.Bounds;
                skBounds = SKRect.Create(
                    0f,
                    0f,
                    Math.Abs(bounds.Left) + bounds.Width,
                    Math.Abs(bounds.Top) + bounds.Height);
            }

            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skBounds);
#if USE_EXPERIMENTAL_LINEAR_RGB
            // TODO:
            using var skPaint = new SKPaint();
            using var skColorFilter = SKColorFilter.CreateTable(null, SvgPaintingExtensions.s_LinearRGBtoSRGB, SvgPaintingExtensions.s_LinearRGBtoSRGB, SvgPaintingExtensions.s_LinearRGBtoSRGB);
            using var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter);
            skPaint.ImageFilter = skImageFilter;
            skCanvas.SaveLayer(skPaint);
#endif
            drawable?.Draw(skCanvas, 0f, 0f);
#if USE_EXPERIMENTAL_LINEAR_RGB
            // TODO:
            skCanvas.Restore();
#endif
            return skPictureRecorder.EndRecording();
        }

        public static SvgDocument? OpenSvg(string path)
        {
            var svgDocument = SvgDocument.Open<SvgDocument>(path, null);
            if (svgDocument != null)
            {
                return svgDocument;
            }
            return null;
        }

        public static SvgDocument? OpenSvgz(string path)
        {
            using (var fileStream = File.OpenRead(path))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var memoryStream = new MemoryStream())
            {
                gzipStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var svgDocument = SvgDocument.Open<SvgDocument>(memoryStream, null);
                if (svgDocument != null)
                {
                    return svgDocument;
                }
            }
            return null;
        }

        public static SvgDocument? Open(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.ToLower() switch
            {
                ".svg" => OpenSvg(path),
                ".svgz" => OpenSvgz(path),
                _ => OpenSvg(path),
            };
        }

        public SKPicture? Picture { get; set; }

        public SKPicture? Load(Stream stream)
        {
            Reset();
            var svgDocument = SvgDocument.Open<SvgDocument>(stream, null);
            if (svgDocument != null)
            {
                Picture = ToPicture(svgDocument);
                return Picture;
            }
            return null;
        }

        public SKPicture? Load(string path)
        {
            Reset();
            var svgDocument = Open(path);
            if (svgDocument != null)
            {
                Picture = ToPicture(svgDocument);
                return Picture;
            }
            return null;
        }

        public SKPicture? FromSvg(string svg)
        {
            Reset();
            var svgDocument = SvgDocument.FromSvg<SvgDocument>(svg);
            if (svgDocument != null)
            {
                Picture = ToPicture(svgDocument);
                return Picture;
            }
            return null;
        }

        public SKPicture? FromSvgDocument(SvgDocument svgDocument)
        {
            Reset();
            if (svgDocument != null)
            {
                Picture = ToPicture(svgDocument);
                return Picture;
            }
            return null;
        }

        public bool Save(Stream stream, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            if (Picture != null)
            {
#if USE_COLORSPACE
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul, SvgPaintingExtensions.Srgb);
#else
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul);
#endif
            }
            return false;
        }

        public bool Save(string path, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            if (Picture != null)
            {
                using var stream = File.OpenWrite(path);
#if USE_COLORSPACE
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul, SvgPaintingExtensions.Srgb);
#else
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul);
#endif
            }
            return false;

        }

        public void Reset()
        {
            if (Picture != null)
            {
                Picture.Dispose();
                Picture = null;
            }
        }

        public void Dispose()
        {
            Reset();
        }
    }
}
