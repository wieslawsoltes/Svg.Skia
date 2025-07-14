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
        using var skPaint = new SkiaSharp.SKPaint
        {
            TextSize = paint.TextSize,
            Typeface = paint.Typeface is { } t
                ? SkiaSharp.SKTypeface.FromFamilyName(
                    t.FamilyName,
                    (int)t.FontWeight,
                    (int)t.FontWidth,
                    (SkiaSharp.SKFontStyleSlant)t.FontSlant)
                : SkiaSharp.SKTypeface.Default
        };

        skPaint.GetFontMetrics(out var skMetrics);
        return new ShimSkiaSharp.SKFontMetrics
        {
            Top = skMetrics.Top,
            Ascent = skMetrics.Ascent,
            Descent = skMetrics.Descent,
            Bottom = skMetrics.Bottom,
            Leading = skMetrics.Leading
        };
    }

    public float MeasureText(string? text, ShimSkiaSharp.SKPaint paint, ref ShimSkiaSharp.SKRect bounds)
    {
        if (text is null)
        {
            bounds = default;
            return 0f;
        }

        using var skPaint = new SkiaSharp.SKPaint
        {
            TextSize = paint.TextSize,
            Typeface = paint.Typeface is { } t
                ? SkiaSharp.SKTypeface.FromFamilyName(
                    t.FamilyName,
                    (int)t.FontWeight,
                    (int)t.FontWidth,
                    (SkiaSharp.SKFontStyleSlant)t.FontSlant)
                : SkiaSharp.SKTypeface.Default
        };

        var skBounds = new SkiaSharp.SKRect();
        var width = skPaint.MeasureText(text, ref skBounds);
        bounds = new ShimSkiaSharp.SKRect(skBounds.Left, skBounds.Top, skBounds.Right, skBounds.Bottom);
        return width;
    }
}
