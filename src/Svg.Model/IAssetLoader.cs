using System.IO;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp;
#endif

namespace Svg.Model;

public interface IAssetLoader
{
    SKImage LoadImage(Stream stream);
    float MeasureText(SKPaint paint, string text);
}
