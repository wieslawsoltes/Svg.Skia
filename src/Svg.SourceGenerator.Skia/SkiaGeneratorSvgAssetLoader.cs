// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;

namespace Svg.SourceGenerator.Skia;

public class SkiaGeneratorSvgAssetLoader : Model.ISvgAssetLoader
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

    public ShimSkiaSharp.SKFontMetrics GetFontMetrics(ShimSkiaSharp.SKPaint paint)
    {
        // TODO: compute metrics using SkiaSharp when native library loading is fixed
        var size = paint.TextSize;
        return new ShimSkiaSharp.SKFontMetrics
        {
            Ascent = -size * 0.8f,
            Descent = size * 0.2f,
            Top = -size * 0.8f,
            Bottom = size * 0.2f,
            Leading = 0f
        };
    }

    public float MeasureText(string? text, ShimSkiaSharp.SKPaint paint, ref ShimSkiaSharp.SKRect bounds)
    {
        // TODO: compute text width using SkiaSharp when native library loading is fixed
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
