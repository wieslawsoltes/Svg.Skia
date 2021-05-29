using System.IO;
using Svg.Model;
using ShimSkiaSharp.Primitives;

namespace Svg.Skia
{
    public class SkiaAssetLoader : IAssetLoader
    {
        public SKImage LoadImage(Stream stream)
        {
            var data = SKImage.FromStream(stream);
            using var image = SkiaSharp.SKImage.FromEncodedData(data);
            return new SKImage
            {
                Data = data,
                Width = image.Width,
                Height = image.Height
            };
        }
    }
}
