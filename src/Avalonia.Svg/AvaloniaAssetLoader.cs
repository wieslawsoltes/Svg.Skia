using System.IO;
using ShimSkiaSharp;
using AMI = Avalonia.Media.Imaging;
using SM = Svg.Model;

namespace Avalonia.Svg
{
    public class AvaloniaAssetLoader : SM.IAssetLoader
    {
        public SKImage LoadImage(Stream stream)
        {
            var data = SKImage.FromStream(stream);
            using var image = new AMI.Bitmap(stream);
            return new SKImage
            {
                Data = data,
                Width = (float)image.Size.Width,
                Height = (float)image.Size.Height
            };
        }
    }
}
