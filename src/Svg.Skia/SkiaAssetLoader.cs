using System.IO;
using SkiaSharp;
using Svg.Model;
using Svg.Model.Primitives;

namespace Svg.Skia
{
    public class SkiaAssetLoader : IAssetLoader
    {
        public Image LoadImage(Stream stream)
        {
            var data = Image.FromStream(stream);
            using var image = SKImage.FromEncodedData(data);
            return new Image
            {
                Data = data,
                Width = image.Width,
                Height = image.Height
            };
        }
    }
}
