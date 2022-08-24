namespace Svg.Skia;

public class SkiaAssetLoader : Svg.Model.IAssetLoader
{
#if USE_SKIASHARP
    public SkiaSharp.SKImage LoadImage(System.IO.Stream stream)
    {
        return SkiaSharp.SKImage.FromEncodedData(stream);
    }

    public float MeasureText(SkiaSharp.SKPaint paint, string text)
    {
        return paint.MeasureText(text);
    }
#else
    public ShimSkiaSharp.SKImage LoadImage(System.IO.Stream stream)
    {
        var data = ShimSkiaSharp.SKImage.FromStream(stream);
        using var image = SkiaSharp.SKImage.FromEncodedData(data);
        return new ShimSkiaSharp.SKImage
        {
            Data = data,
            Width = image.Width,
            Height = image.Height
        };
    }

    public float MeasureText(ShimSkiaSharp.SKPaint paint, string text)
    {
        using var skPaint = paint.ToSKPaint();
        return skPaint.MeasureText(text);
    }
#endif
}
