using System;
using System.IO;
using System.Linq;

namespace Svg.Skia.TypefaceProviders;

public sealed class CustomTypefaceProvider : ITypefaceProvider, IDisposable
{
    public static readonly char[] s_fontFamilyTrim = { '\'' };

    public SkiaSharp.SKTypeface? Typeface { get; set; }

    public string FamilyName { get; set; }

    public CustomTypefaceProvider(Stream stream, int index = 0)
    {
        Typeface = SkiaSharp.SKTypeface.FromStream(stream, index);
        FamilyName = Typeface.FamilyName;
    }

    public CustomTypefaceProvider(SkiaSharp.SKStreamAsset stream, int index = 0)
    {
        Typeface = SkiaSharp.SKTypeface.FromStream(stream, index);
        FamilyName = Typeface.FamilyName;
    }

    public CustomTypefaceProvider(string path, int index = 0)
    {
        Typeface = SkiaSharp.SKTypeface.FromFile(path, index);
        FamilyName = Typeface.FamilyName;
    }

    public CustomTypefaceProvider(SkiaSharp.SKData data, int index = 0)
    {
        Typeface = SkiaSharp.SKTypeface.FromData(data, index);
        FamilyName = Typeface.FamilyName;
    }

    public SkiaSharp.SKTypeface? FromFamilyName(string fontFamily, SkiaSharp.SKFontStyleWeight fontWeight, SkiaSharp.SKFontStyleWidth fontWidth, SkiaSharp.SKFontStyleSlant fontStyle)
    {
        if (Typeface is null)
        {
            return null;
        }
        var skTypeface = default(SkiaSharp.SKTypeface);
        var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
        if (fontFamilyNames is { } && fontFamilyNames.Length > 0)
        {
            foreach (var fontFamilyName in fontFamilyNames)
            {
                if (fontFamilyName == FamilyName 
                    && Typeface.FontStyle.Width == (int)fontWidth
                    && Typeface.FontStyle.Weight == (int)fontWeight
                    && Typeface.FontStyle.Slant == fontStyle)
                {
                    skTypeface = Typeface;
                    break;
                }
            }
        }
        return skTypeface;
    }

    public void Dispose()
    {
        Typeface?.Dispose();
        Typeface = null;
    }
}
