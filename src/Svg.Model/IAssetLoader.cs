using System.Collections.Generic;
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
    IEnumerable<(string text, float advance, SKTypeface? typeface)>
        FindTypefaces(string text, SKPaint paintPreferredTypeface);
}
