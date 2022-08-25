using System.Collections.Generic;
using System.IO;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp;
#endif

namespace Svg.Model;

public record struct TypefaceSpan(string Text, float Advance, SKTypeface? Typeface);

public interface IAssetLoader
{
    SKImage LoadImage(Stream stream);
    List<TypefaceSpan> FindTypefaces(string text, SKPaint paintPreferredTypeface);
}
