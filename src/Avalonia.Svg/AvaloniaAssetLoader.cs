using System.IO;
using ShimSkiaSharp;
using AMI = Avalonia.Media.Imaging;
using SM = Svg.Model;
using Avalonia.Media;
using System.Collections.Generic;

namespace Avalonia.Svg;

public class AvaloniaAssetLoader : SM.IAssetLoader
{
    public SKImage LoadImage(Stream stream)
    {
        var data = SKImage.FromStream(stream);
        using var image = new AMI.Bitmap(stream);
        return new SKImage {Data = data, Width = (float)image.Size.Width, Height = (float)image.Size.Height};
    }

    public List<SM.TypefaceSpan> FindTypefaces(string text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        var ret = new List<SM.TypefaceSpan>();

        System.Func<int, Typeface?> matchCharacter;

        if (paintPreferredTypeface.Typeface is { } preferredTypeface)
        {
            var weight = preferredTypeface.FontWeight.ToFontWeight();
            var width = preferredTypeface.FontWidth.ToFontStretch();
            var slant = preferredTypeface.Style.ToFontStyle();

            matchCharacter = codepoint =>
            {
                FontManager.Current.TryMatchCharacter(
                    codepoint,
                    slant,
                    weight,
                    width,
                    preferredTypeface.FamilyName is { } n 
                        ? FontFamily.Parse(n) 
                        : null,
                    null, out var typeface);

                return typeface;
            };
        }
        else
        {
            matchCharacter = codepoint =>
            {
                FontManager.Current.TryMatchCharacter(
                    codepoint,
                    FontStyle.Normal,
                    FontWeight.Normal,
                    FontStretch.Normal,
                    null,
                    null,
                    out var typeface
                );

                return typeface;
            };
        }

        var runningTypeface = paintPreferredTypeface.Typeface.ToTypeface();
        var runningAdvance = 0f;
        var currentTypefaceStartIndex = 0;
        var i = 0;

        void YieldCurrentTypefaceText()
        {
            var currentTypefaceText = text.Substring(currentTypefaceStartIndex, i - currentTypefaceStartIndex);

            ret.Add(
                new(
                    currentTypefaceText,
                    runningAdvance * paintPreferredTypeface.TextSize,
                    runningTypeface is not { } typeface
                        ? null
                        : ShimSkiaSharp.SKTypeface.FromFamilyName(
                            typeface.FontFamily.Name,
                            typeface.Weight.ToSKFontWeight(),
                            typeface.Stretch.ToSKFontStretch(),
                            typeface.Style.ToSKFontStyle()
                        )));
        }

        for (; i < text.Length; i++)
        {
            var codepoint = char.ConvertToUtf32(text, i);
            var typeface = matchCharacter(codepoint);
            if (i == 0)
            {
                runningTypeface = typeface;
            }
            else if (runningTypeface is null
                     && typeface is { }
                     || runningTypeface is { }
                     && typeface is null
                     || runningTypeface != typeface)
            {
                YieldCurrentTypefaceText();
                runningAdvance = 0;
                currentTypefaceStartIndex = i;
                runningTypeface = typeface;
            }

            var glyphTypeface = (typeface ?? Typeface.Default).GlyphTypeface;
            runningAdvance += glyphTypeface.GetGlyphAdvance(glyphTypeface.GetGlyph((uint)codepoint));

            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }

        YieldCurrentTypefaceText();

        return ret;
    }
}
