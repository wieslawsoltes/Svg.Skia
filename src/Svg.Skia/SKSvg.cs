// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#define USE_DRAWABLES
using System;
using System.IO;
using System.IO.Compression;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SKPictureExtensions
    {
        public static SKBitmap? ToBitmap(this SKPicture sKPicture, SKColor background, float scaleX, float scaleY)
        {
            float width = sKPicture.CullRect.Width * scaleX;
            float height = sKPicture.CullRect.Height * scaleY;
            if (width > 0 && height > 0)
            {
                var skImageInfo = new SKImageInfo((int)width, (int)height);
                var skBitmap = new SKBitmap(skImageInfo);
                using (var skCanvas = new SKCanvas(skBitmap))
                {
                    skCanvas.Clear(SKColors.Transparent);
                    if (background != SKColor.Empty)
                    {
                        skCanvas.DrawColor(background);
                    }
                    skCanvas.Save();
                    skCanvas.Scale(scaleX, scaleY);
                    skCanvas.DrawPicture(sKPicture);
                    skCanvas.Restore();
                    return skBitmap;
                }
            }
            return null;
        }
    }

    public class SKSvg : IDisposable
    {
        public static SvgDocument? OpenSvg(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var svgDocument = SvgDocument.Open<SvgDocument>(path, null);
            if (svgDocument != null)
            {
                svgDocument.FlushStyles(true);
                return svgDocument;
            }
            return null;
        }

        public static SvgDocument? OpenSvgz(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            using (var fileStream = File.OpenRead(path))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var memoryStream = new MemoryStream())
            {
                gzipStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var svgDocument = SvgDocument.Open<SvgDocument>(memoryStream, null);
                if (svgDocument != null)
                {
                    svgDocument.FlushStyles(true);
                    return svgDocument;
                }
            }
            return null;
        }

        public static SvgDocument? Open(string path)
        {
            var extension = Path.GetExtension(path);
            switch (extension.ToLower())
            {
                default:
                case ".svg":
                    return OpenSvg(path);
                case ".svgz":
                    return OpenSvgz(path);
            }
        }

        public static bool Save(Stream stream, SKPicture sKPicture, SKColor background, SKEncodedImageFormat format, int quality, float scaleX, float scaleY)
        {
            using (var skBitmap = sKPicture.ToBitmap(background, scaleX, scaleY))
            {
                if (skBitmap == null)
                {
                    return false;
                }
                using (var skImage = SKImage.FromBitmap(skBitmap))
                using (var skData = skImage.Encode(format, quality))
                {
                    if (skData != null)
                    {
                        skData.SaveTo(stream);
                        return true;
                    }
                }
            }
            return false;
        }

        public static void Draw(SKCanvas skCanvas, SvgFragment svgFragment)
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            var skBounds = SKRect.Create(skSize);
#if USE_DRAWABLES
            using (var drawable = DrawableFactory.Create(svgFragment, skBounds, false))
            {
                drawable?.Draw(skCanvas, 0f, 0f);
            }
#else
            using (var renderer = new SKSvgRenderer(skCanvas, skSize))
            {
                renderer.DrawFragment(svgFragment, skBounds, false);
            }
#endif
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
            using (var skPictureRecorder = new SKPictureRecorder())
            using (var skCanvas = skPictureRecorder.BeginRecording(skBounds))
#if USE_DRAWABLES
            using (var drawable = DrawableFactory.Create(svgFragment, skBounds, false))
            {
                drawable?.Draw(skCanvas, 0f, 0f);
                return skPictureRecorder.EndRecording();
            }
#else
            using (var renderer = new SKSvgRenderer(skCanvas, skSize))
            {
                renderer.DrawFragment(svgFragment, skBounds, false);
                return skPictureRecorder.EndRecording();
            }
#endif
        }

        public static SKDrawable? ToDrawable(SvgFragment svgFragment)
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            var skBounds = SKRect.Create(skSize);
            using (var skPictureRecorder = new SKPictureRecorder())
            using (var skCanvas = skPictureRecorder.BeginRecording(skBounds))
#if USE_DRAWABLES
            {
                return DrawableFactory.Create(svgFragment, skBounds, false);
            }
#else
            using (var renderer = new SKSvgRenderer(skCanvas, skSize))
            {
                renderer.DrawFragment(svgFragment, skBounds, false);
                return skPictureRecorder.EndRecordingAsDrawable();
            }
#endif
        }

        public SKPicture? Picture { get; set; }

        public SKPicture? Load(Stream stream)
        {
            Reset();
            var svgDocument = SvgDocument.Open<SvgDocument>(stream, null);
            if (svgDocument != null)
            {
                svgDocument.FlushStyles(true);
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
                svgDocument.FlushStyles(true);
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
                svgDocument.FlushStyles(true);
                Picture = ToPicture(svgDocument);
                return Picture;
            }
            return null;
        }

        public bool Save(Stream stream, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            if (Picture != null)
            {
                return Save(stream, Picture, background, format, quality, scaleX, scaleY);
            }
            return false;
        }

        public bool Save(string path, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            if (Picture != null)
            {
                using (var stream = File.OpenWrite(path))
                {
                    return Save(stream, Picture, background, format, quality, scaleX, scaleY);
                }
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
