using System.Collections.Generic;
using SkiaSharp;
using static SkiaSharp.HarfBuzz.SKShaper;

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

    public IEnumerable<(string text, float advance, ShimSkiaSharp.SKTypeface? typeface)>
        FindTypefaces(string text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        System.Func<int, SKTypeface?> matchCharacter;
        if (paintPreferredTypeface.Typeface is { } preferredTypeface)
        {
            var weight = preferredTypeface.FontWeight.ToSKFontStyleWeight();
            var width = preferredTypeface.FontWidth.ToSKFontStyleWidth();
            var slant = preferredTypeface.Style.ToSKFontStyleSlant();
            matchCharacter = codepoint => SKFontManager.Default.MatchCharacter(
                preferredTypeface.FamilyName, weight, width, slant, null, codepoint);
        } else matchCharacter = codepoint => SKFontManager.Default.MatchCharacter(codepoint);
        using var runningPaint = paintPreferredTypeface.ToSKPaint();
        var currentTypefaceStartIndex = 0;
        var i = 0;
        (string text, float advance, ShimSkiaSharp.SKTypeface? typeface) YieldCurrentTypefaceText()
        {
            var currentTypefaceText = text.Substring(currentTypefaceStartIndex, i - currentTypefaceStartIndex);
            return (currentTypefaceText, runningPaint.MeasureText(currentTypefaceText),
                runningPaint.Typeface is null ? null :
                ShimSkiaSharp.SKTypeface.FromFamilyName(
                    runningPaint.Typeface.FamilyName,
                    // SkiaSharp provides int properties here. Let's just assume our
                    // ShimSkiaSharp defines the same values as SkiaSharp and convert directly
                    (ShimSkiaSharp.SKFontStyleWeight)runningPaint.Typeface.FontWeight,
                    (ShimSkiaSharp.SKFontStyleWidth)runningPaint.Typeface.FontWidth,
                    (ShimSkiaSharp.SKFontStyleSlant)runningPaint.Typeface.FontSlant
                ));
        }
        for (; i < text.Length; i++)
        {
            var typeface = matchCharacter(char.ConvertToUtf32(text, i));
            if (i == 0)
                runningPaint.Typeface = typeface;
            else if (runningPaint.Typeface is null && typeface is { }
                || runningPaint.Typeface is { } && typeface is null
                || runningPaint.Typeface is { } l && typeface is { } r
                   && (l.FamilyName, l.FontWeight, l.FontWidth, l.FontSlant)
                   != (r.FamilyName, r.FontWeight, r.FontWidth, r.FontSlant))
            {
                yield return YieldCurrentTypefaceText();
                currentTypefaceStartIndex = i;
                runningPaint.Typeface = typeface;
            }

            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }
        yield return YieldCurrentTypefaceText();
    }
#endif
}
