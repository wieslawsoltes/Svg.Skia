using System.Collections.Generic;
using System.IO;

namespace svgc;

internal class ImageSharpAssetLoader : Svg.Model.ISvgAssetLoader
{
    public ShimSkiaSharp.SKImage LoadImage(Stream stream)
    {
        var data = ShimSkiaSharp.SKImage.FromStream(stream);
        using var image = SixLabors.ImageSharp.Image.Load(data);
        return new ShimSkiaSharp.SKImage { Data = data, Width = image.Width, Height = image.Height };
    }

    public List<Svg.Model.TypefaceSpan> FindTypefaces(string? text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        if (text is null || string.IsNullOrEmpty(text))
        {
            return new List<Svg.Model.TypefaceSpan>();
        }

        // TODO:
        // Font fallback and text advancing code should be generated along with canvas commands instead.
        // Otherwise, some package reference hacking may be needed.
        return new List<Svg.Model.TypefaceSpan>
        {
            new(text, text.Length * paintPreferredTypeface.TextSize, paintPreferredTypeface.Typeface)
        };
    }

    public ShimSkiaSharp.SKFontMetrics GetFontMetrics(ShimSkiaSharp.SKPaint paint)
    {
        // TODO: provide real font metrics once ImageSharp exposes the APIs
        var size = paint.TextSize;
        return new ShimSkiaSharp.SKFontMetrics
        {
            Ascent = -size * 0.8f,
            Descent = size * 0.2f,
            Top = -size * 0.8f,
            Bottom = size * 0.2f,
            Leading = 0f,
            UnderlinePosition = 0f,
            UnderlineThickness = 0f,
            StrikeoutPosition = 0f,
            StrikeoutThickness = 0f
        };
    }

    public float MeasureText(string? text, ShimSkiaSharp.SKPaint paint, ref ShimSkiaSharp.SKRect bounds)
    {
        // TODO: provide real text measurement once ImageSharp exposes the APIs
        if (string.IsNullOrEmpty(text))
        {
            bounds = default;
            return 0f;
        }

        var size = paint.TextSize;
        var width = text.Length * size * 0.6f;
        bounds = new ShimSkiaSharp.SKRect(0, -size * 0.8f, width, size * 0.2f);
        return width;
    }

    public ShimSkiaSharp.SKPath? GetTextPath(string? text, ShimSkiaSharp.SKPaint paint, float x, float y)
    {
        return null;
    }
}
