using System.Collections.Generic;
using System;
using System.IO;
using ShimSkiaSharp;
using AMI = Avalonia.Media.Imaging;
using SM = Svg.Model;
using Avalonia.Media;

namespace Avalonia.Svg;

public class AvaloniaAssetLoader : SM.IAssetLoader
{
    public SKImage LoadImage(Stream stream)
    {
        var data = SKImage.FromStream(stream);
        using var image = new AMI.Bitmap(stream);
        return new SKImage
        {
            Data = data,
            Width = (float)image.Size.Width,
            Height = (float)image.Size.Height
        };
    }

    public float MeasureText(SKPaint paint, string text)
    {
        var result = 0f;
        var typeface = (paint.Typeface.ToTypeface() ?? Typeface.Default).GlyphTypeface;
        for (int i = 0; i < text.Length; i++)
        {
            result += typeface.GetGlyphAdvance(typeface.GetGlyph((uint)char.ConvertToUtf32(text, i))) * paint.TextSize;
            if (char.IsHighSurrogate(text[i]))
                i++;
        }
        return result;
    }
}
