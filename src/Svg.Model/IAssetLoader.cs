using System.IO;
using Svg.Model.Primitives;

namespace Svg.Model
{
    public interface IAssetLoader
    {
        SKImage LoadImage(Stream stream);
    }
}
