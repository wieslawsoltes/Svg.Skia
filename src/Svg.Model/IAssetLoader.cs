using System.IO;
using ShimSkiaSharp.Primitives;

namespace Svg.Model
{
    public interface IAssetLoader
    {
        SKImage LoadImage(Stream stream);
    }
}
