using System.IO;
using Svg.Model.Primitives;
using AMI = Avalonia.Media.Imaging;
using SM = Svg.Model;

namespace Avalonia.Svg
{
    public class AvaloniaAssetLoader : SM.IAssetLoader
    {
        public Image LoadImage(Stream stream)
        {
            var data = Image.FromStream(stream);
            using var image = new AMI.Bitmap(stream);
            return new Image()
            {
                Data = data,
                Width = (float)image.Size.Width,
                Height = (float)image.Size.Height
            };
        }
    }
}
