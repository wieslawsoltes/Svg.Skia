using System.IO;
using Svg.Model;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp;
#endif

namespace Svg.Skia;

public class SkiaAssetLoader : IAssetLoader
{
#if USE_SKIASHARP
    public SKImage LoadImage(Stream stream)
    {
        return SKImage.FromEncodedData(stream);
    }
#else
    public SKImage LoadImage(Stream stream)
    {
        var data = SKImage.FromStream(stream);
        using var image = SkiaSharp.SKImage.FromEncodedData(data);
        return new SKImage
        {
            Data = data,
            Width = image.Width,
            Height = image.Height
        };
    }
#endif
    public float MeasureText(SKPaint paint, string text)
    {
        using var skPaint = paint.ToSKPaint();
        return skPaint.MeasureText(text);
    }
}
