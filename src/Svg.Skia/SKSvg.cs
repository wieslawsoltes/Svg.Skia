using System;
using System.IO;
using System.IO.Compression;
using SkiaSharp;
#if USE_PICTURE
using SP = Svg.Picture;
using SPS = Svg.Picture.Skia;
#endif

namespace Svg.Skia
{
    public partial class SKSvg
    {
        static SKSvg()
        {
            SvgDocument.SkipGdiPlusCapabilityCheck = true;
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
            var extension = System.IO.Path.GetExtension(path);
            return extension.ToLower() switch
            {
                ".svg" => OpenSvg(path),
                ".svgz" => OpenSvgz(path),
                _ => OpenSvg(path),
            };
        }

        public static SvgDocument? Open(Stream stream)
        {
            return SvgDocument.Open<SvgDocument>(stream, null);
        }
    }

#if USE_PICTURE
    public partial class SKSvg
    {
        public static SP.Picture? ToModel(SvgFragment svgFragment)
        {
            var size = SvgExtensions.GetDimensions(svgFragment);
            var bounds = SP.Rect.Create(size);
            using var drawable = DrawableFactory.Create(svgFragment, bounds, null, Attributes.None);
            if (drawable == null)
            {
                return null;
            }
            drawable.PostProcess();

            if (bounds.IsEmpty)
            {
                var drawableBounds = drawable.Bounds;
                bounds = SP.Rect.Create(
                    0f,
                    0f,
                    Math.Abs(drawableBounds.Left) + drawableBounds.Width,
                    Math.Abs(drawableBounds.Top) + drawableBounds.Height);
            }

            return drawable.Snapshot(bounds);
        }
    }
#endif

    public partial class SKSvg
    {
#if USE_PICTURE
        public static SKPicture? ToPicture(SvgFragment svgFragment)
        {
            var picture = ToModel(svgFragment);
            if (picture != null)
            {
                return SPS.SkiaPicture.Record(picture);
            }
            return null;
        }
#else
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
                var drawableBounds = drawable.Bounds;
                skBounds = SKRect.Create(
                    0f,
                    0f,
                    Math.Abs(drawableBounds.Left) + drawableBounds.Width,
                    Math.Abs(drawableBounds.Top) + drawableBounds.Height);
            }

            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skBounds);
            drawable?.Draw(skCanvas, 0f, 0f);
            return skPictureRecorder.EndRecording();
        }
#endif
#if USE_PICTURE
        public static void Draw(SKCanvas skCanvas, SvgFragment svgFragment)
        {
            var size = SvgExtensions.GetDimensions(svgFragment);
            var bounds = SP.Rect.Create(size);
            using var drawable = DrawableFactory.Create(svgFragment, bounds, null, Attributes.None);
            if (drawable != null)
            {
                drawable.PostProcess();
                var picture = drawable.Snapshot(bounds);
                if (picture != null)
                {
                    SPS.SkiaPicture.Draw(picture, skCanvas);
                }
            }
        }
#else
        public static void Draw(SKCanvas skCanvas, SvgFragment svgFragment)
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            var skBounds = SKRect.Create(skSize);
            using var drawable = DrawableFactory.Create(svgFragment, skBounds, null, Attributes.None);
            if (drawable != null)
            {
                drawable.PostProcess();
                drawable.Draw(skCanvas, 0f, 0f);
            }
        }
#endif
        public static void Draw(SKCanvas skCanvas, string path)
        {
            var svgDocument = Open(path);
            if (svgDocument != null)
            {
                Draw(skCanvas, svgDocument);
            }
        }
    }

    public partial class SKSvg : IDisposable
    {
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
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul, SKSvgSettings.s_srgb);
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
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul, SKSvgSettings.s_srgb);
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
