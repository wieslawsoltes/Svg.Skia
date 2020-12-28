using System.IO;
using AMI = Avalonia.Media.Imaging;
using SM = Svg.Model;

namespace Avalonia.Svg
{
    public class AvaloniaAssetLoader : SM.IAssetLoader
    {
        public SM.Image LoadImage(Stream stream)
        {
            var data = SM.Image.FromStream(stream);
            using var image = new AMI.Bitmap(stream);
            return new SM.Image()
            {
                Data = data,
                Width = (float)image.Size.Width,
                Height = (float)image.Size.Height
            };
        }
    }
}
