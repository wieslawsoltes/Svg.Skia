using System.Collections.Generic;

namespace Svg.SourceGenerator.Skia;

public class SkiaGeneratorAssetLoader : Model.IAssetLoader
{
    public ShimSkiaSharp.SKImage LoadImage(System.IO.Stream stream)
    {
        var data = ShimSkiaSharp.SKImage.FromStream(stream);
        using var image = SkiaSharp.SKImage.FromEncodedData(data);
        return new ShimSkiaSharp.SKImage {Data = data, Width = image.Width, Height = image.Height};
    }

    public List<Model.TypefaceSpan> FindTypefaces(string? text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        if (text is null || string.IsNullOrEmpty(text))
        {
            return new List<Model.TypefaceSpan>();
        }

        // TODO:
        // Font fallback and text advancing code should be generated along with canvas commands instead.
        // Otherwise, some package reference hacking may be needed.
        return new List<Model.TypefaceSpan>
        {
            new(text, text.Length * paintPreferredTypeface.TextSize, paintPreferredTypeface.Typeface)
        };
    }
}
