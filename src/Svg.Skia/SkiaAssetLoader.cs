using System.Collections.Generic;
using SkiaSharp;
using static SkiaSharp.HarfBuzz.SKShaper;
#if USE_SKIASHARP
using ShimSkiaSharp = SkiaSharp;
#endif

namespace Svg.Skia;

public class SkiaAssetLoader : Svg.Model.IAssetLoader
{
    public ShimSkiaSharp.SKImage LoadImage(System.IO.Stream stream)
    {
#if USE_SKIASHARP
        return SkiaSharp.SKImage.FromEncodedData(stream);
#else
        var data = ShimSkiaSharp.SKImage.FromStream(stream);
        using var image = SkiaSharp.SKImage.FromEncodedData(data);
        return new ShimSkiaSharp.SKImage
        {
            Data = data,
            Width = image.Width,
            Height = image.Height
        };
#endif
    }

    public List<(string text, float advance, ShimSkiaSharp.SKTypeface? typeface)>
        FindTypefaces(string text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        var ret = new List<(string text, float advance, ShimSkiaSharp.SKTypeface? typeface)>();
        System.Func<int, SKTypeface?> matchCharacter;
        if (paintPreferredTypeface.Typeface is { } preferredTypeface)
        {
#if USE_SKIASHARP
            var weight = preferredTypeface.FontWeight;
            var width = preferredTypeface.FontWidth;
            var slant = preferredTypeface.Style;
#else
            var weight = preferredTypeface.FontWeight.ToSKFontStyleWeight();
            var width = preferredTypeface.FontWidth.ToSKFontStyleWidth();
            var slant = preferredTypeface.Style.ToSKFontStyleSlant();
#endif
            matchCharacter = codepoint => SKFontManager.Default.MatchCharacter(
                preferredTypeface.FamilyName, weight, width, slant, null, codepoint);
        } else matchCharacter = codepoint => SKFontManager.Default.MatchCharacter(codepoint);
#if USE_SKIASHARP
        using var runningPaint = paintPreferredTypeface.Clone();
#else
        using var runningPaint = paintPreferredTypeface.ToSKPaint();
#endif
        var currentTypefaceStartIndex = 0;
        var i = 0;
        void YieldCurrentTypefaceText()
        {
            var currentTypefaceText = text.Substring(currentTypefaceStartIndex, i - currentTypefaceStartIndex);
            ret.Add((currentTypefaceText, runningPaint.MeasureText(currentTypefaceText),
                runningPaint.Typeface is null ? null :
                ShimSkiaSharp.SKTypeface.FromFamilyName(
                    runningPaint.Typeface.FamilyName,
                    // SkiaSharp provides int properties here. Let's just assume our
                    // ShimSkiaSharp defines the same values as SkiaSharp and convert directly
                    (ShimSkiaSharp.SKFontStyleWeight)runningPaint.Typeface.FontWeight,
                    (ShimSkiaSharp.SKFontStyleWidth)runningPaint.Typeface.FontWidth,
                    (ShimSkiaSharp.SKFontStyleSlant)runningPaint.Typeface.FontSlant
                )));
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
                YieldCurrentTypefaceText();
                currentTypefaceStartIndex = i;
                runningPaint.Typeface = typeface;
            }

            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }
        YieldCurrentTypefaceText();
        return ret;
    }
}
