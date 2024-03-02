using System.Collections.Generic;
using System.IO;

namespace svgc;

internal class ImageSharpAssetLoader : Svg.Model.IAssetLoader
{
    public ShimSkiaSharp.SKImage LoadImage(Stream stream)
    {
        var data = ShimSkiaSharp.SKImage.FromStream(stream);
        using var image = SixLabors.ImageSharp.Image.Load(data);
        return new ShimSkiaSharp.SKImage {Data = data, Width = image.Width, Height = image.Height};
    }

    public List<Svg.Model.TypefaceSpan> FindTypefaces(string? text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        if (text is null || string.IsNullOrEmpty(text))
        {
            return [];
        }

        // TODO:
        // Font fallback and text advancing code should be generated along with canvas commands instead.
        // Otherwise, some package reference hacking may be needed.
        return [new(text, text.Length * paintPreferredTypeface.TextSize, paintPreferredTypeface.Typeface)];
    }
}
