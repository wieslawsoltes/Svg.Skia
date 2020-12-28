using System.IO;
using Svg.Model.Primitives;

namespace Svg.Model
{
    public interface IAssetLoader
    {
        Image LoadImage(Stream stream);
    }
}
