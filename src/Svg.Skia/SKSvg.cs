using System;
using SkiaSharp;
using Svg.Model;
using Svg.Model.Drawables;
using Svg.Model.Primitives;

namespace Svg.Skia
{
    public class SKSvg : IDisposable
    {
        private static readonly IAssetLoader _assetLoader = new SkiaAssetLoader();

        public static SKPicture? ToPicture(SvgFragment svgFragment)
        {
            var picture = SvgModelExtensions.ToModel(svgFragment, _assetLoader);
            return picture?.ToSKPicture();
        }

        public static void Draw(SKCanvas skCanvas, SvgFragment svgFragment)
        {
            var size = SvgModelExtensions.GetDimensions(svgFragment);
            var bounds = Rect.Create(size);
            var drawable = DrawableFactory.Create(svgFragment, bounds, null, _assetLoader);
            if (drawable is { })
            {
                drawable.PostProcess();
                var picture = drawable.Snapshot(bounds);
                picture.Draw(skCanvas);
            }
        }

        public static void Draw(SKCanvas skCanvas, string path)
        {
            var svgDocument = SvgModelExtensions.Open(path);
            if (svgDocument is { })
            {
                Draw(skCanvas, svgDocument);
            }
        }

        public Picture? Model { get; set; }
        
        public SKPicture? Picture { get; set; }

        public SKPicture? Load(System.IO.Stream stream)
        {
            Reset();
            var svgDocument = SvgModelExtensions.Open(stream);
            if (svgDocument is { })
            {
                Picture = ToPicture(svgDocument);
                return Picture;
            }
            return null;
        }

        public SKPicture? Load(string path)
        {
            Reset();
            var svgDocument = SvgModelExtensions.Open(path);
            if (svgDocument is { })
            {
                Picture = ToPicture(svgDocument);
                return Picture;
            }
            return null;
        }

        public SKPicture? FromSvg(string svg)
        {
            Reset();
            var svgDocument = SvgModelExtensions.FromSvg(svg);
            if (svgDocument is { })
            {
                Picture = ToPicture(svgDocument);
                return Picture;
            }
            return null;
        }

        public SKPicture? FromSvgDocument(SvgDocument? svgDocument)
        {
            Reset();
            if (svgDocument is { })
            {
                Picture = ToPicture(svgDocument);
                return Picture;
            }
            return null;
        }

        public bool Save(System.IO.Stream stream, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            if (Picture is { })
            {
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul, SKSvgSettings.s_srgb);
            }
            return false;
        }

        public bool Save(string path, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            if (Picture is { })
            {
                using var stream = System.IO.File.OpenWrite(path);
                return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul, SKSvgSettings.s_srgb);
            }
            return false;
        }

        private void Reset()
        {
            Model = null;

            Picture?.Dispose();
            Picture = null;
        }

        public void Dispose()
        {
            Reset();
        }
    }
}
