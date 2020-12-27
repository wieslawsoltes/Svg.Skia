using System;
using System.IO.Compression;
using SkiaSharp;
using Svg.Model;

namespace Svg.Skia
{
    public class SKSvg : IDisposable
    {
        static SKSvg()
        {
            SvgDocument.SkipGdiPlusCapabilityCheck = true;
            SvgDocument.PointsPerInch = 96;
        }

        public static SvgDocument? OpenSvg(string path)
        {
            return SvgDocument.Open<SvgDocument>(path, null);
        }

        public static SvgDocument? OpenSvgz(string path)
        {
            using var fileStream = System.IO.File.OpenRead(path);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new System.IO.MemoryStream();

            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var svgDocument = SvgDocument.Open<SvgDocument>(memoryStream, null);

            return svgDocument;
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

        public static SvgDocument? Open(System.IO.Stream stream)
        {
            return SvgDocument.Open<SvgDocument>(stream, null);
        }

        public static Picture? ToModel(SvgFragment svgFragment)
        {
            var size = SvgExtensions.GetDimensions(svgFragment);
            var bounds = Rect.Create(size);
            using var drawable = DrawableFactory.Create(svgFragment, bounds, null, Attributes.None);
            if (drawable == null)
            {
                return null;
            }
            drawable.PostProcess();

            if (bounds.IsEmpty)
            {
                var drawableBounds = drawable.Bounds;
                bounds = Rect.Create(
                    0f,
                    0f,
                    Math.Abs(drawableBounds.Left) + drawableBounds.Width,
                    Math.Abs(drawableBounds.Top) + drawableBounds.Height);
            }

            return drawable.Snapshot(bounds);
        }

        public static SKPicture? ToPicture(SvgFragment svgFragment)
        {
            var picture = ToModel(svgFragment);
            if (picture != null)
            {
                return SkiaPicture.Record(picture);
            }
            return null;
        }

        public static void Draw(SKCanvas skCanvas, SvgFragment svgFragment)
        {
            var size = SvgExtensions.GetDimensions(svgFragment);
            var bounds = Rect.Create(size);
            using var drawable = DrawableFactory.Create(svgFragment, bounds, null, Attributes.None);
            if (drawable != null)
            {
                drawable.PostProcess();
                var picture = drawable.Snapshot(bounds);
                if (picture != null)
                {
                    SkiaPicture.Draw(picture, skCanvas);
                }
            }
        }

        public static void Draw(SKCanvas skCanvas, string path)
        {
            var svgDocument = Open(path);
            if (svgDocument != null)
            {
                Draw(skCanvas, svgDocument);
            }
        }

        public SKPicture? Picture { get; set; }

        public SKPicture? Load(System.IO.Stream stream)
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

        public bool Save(System.IO.Stream stream, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            if (Picture != null)
            {
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul, SKSvgSettings.s_srgb);
            }
            return false;
        }

        public bool Save(string path, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            if (Picture != null)
            {
                using var stream = System.IO.File.OpenWrite(path);
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul, SKSvgSettings.s_srgb);
            }
            return false;
        }

        public void Reset()
        {
            Picture?.Dispose();
            Picture = null;
        }

        public void Dispose()
        {
            Reset();
        }
    }
}
