using System.IO;

namespace Svg.Model
{
    public interface IAssetLoader
    {
        Image LoadImage(Stream stream);
    }
}
