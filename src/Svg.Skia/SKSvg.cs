// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.IO.Compression;
using SkiaSharp;

namespace Svg.Skia
{
    public class SKSvg : IDisposable
    {
        public static SKAlphaType s_alphaType = SKAlphaType.Unpremul; // SKAlphaType.Unpremul, SKAlphaType.Premul

        public static SKColorType s_colorType = SKImageInfo.PlatformColorType; // SKImageInfo.PlatformColorType, SKColorType.RgbaF16

        static SKSvg()
        {
            SvgDocument.SkipGdiPlusCapabilityCheck = true;
        }

        public static SKRect GetBounds(Drawable drawable)
        {
            var skBounds = drawable.Bounds;
            return SKRect.Create(
                0f,
                0f,
                Math.Abs(skBounds.Left) + skBounds.Width,
                Math.Abs(skBounds.Top) + skBounds.Height);
        }

        public static void Draw(SKCanvas skCanvas, SvgFragment svgFragment)
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            var skBounds = SKRect.Create(skSize);
            using var drawable = DrawableFactory.Create(svgFragment, skBounds, null, null, Attributes.None);
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

            using var drawable = DrawableFactory.Create(svgFragment, skBounds, null, null, Attributes.None);
            if (drawable == null)
            {
                return null;
            }

            drawable.PostProcess();

            if (skBounds.IsEmpty)
            {
                skBounds = GetBounds(drawable);
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

        public static SKPicture? ToPicture(SvgFragment svgFragment, out Drawable? drawable)
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            var skBounds = SKRect.Create(skSize);

            drawable = DrawableFactory.Create(svgFragment, skBounds, null, null, Attributes.None);
            if (drawable == null)
            {
                return null;
            }

            drawable.PostProcess();

            if (skBounds.IsEmpty)
            {
                skBounds = GetBounds(drawable);
            }

            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skBounds);
            drawable?.Draw(skCanvas, 0f, 0f);
            return skPictureRecorder.EndRecording();
        }

        public static Drawable? ToDrawable(SvgFragment svgFragment)
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            var skBounds = SKRect.Create(skSize);
            var drawable = DrawableFactory.Create(svgFragment, skBounds, null, null, Attributes.None);
            drawable?.PostProcess();
            return drawable;
        }

        public static Drawable? ToDrawable(SvgElement svgElement, SKRect skBounds, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = DrawableFactory.Create(svgElement, skBounds, null, null, ignoreAttributes);
            drawable?.PostProcess();
            return drawable;
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
