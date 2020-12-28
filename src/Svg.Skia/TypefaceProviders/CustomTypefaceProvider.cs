using System;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace Svg.Skia.TypefaceProviders
{
    public sealed class CustomTypefaceProvider : ITypefaceProvider, IDisposable
    {
        public static readonly char[] s_fontFamilyTrim = { '\'' };

        public SKTypeface? Typeface { get; set; }

        public string FamilyName { get; set; }

        public CustomTypefaceProvider(Stream stream, int index = 0)
        {
            Typeface = SKTypeface.FromStream(stream, index);
            FamilyName = Typeface.FamilyName;
        }

        public CustomTypefaceProvider(SKStreamAsset stream, int index = 0)
        {
            Typeface = SKTypeface.FromStream(stream, index);
            FamilyName = Typeface.FamilyName;
        }

        public CustomTypefaceProvider(string path, int index = 0)
        {
            Typeface = SKTypeface.FromFile(path, index);
            FamilyName = Typeface.FamilyName;
        }

        public CustomTypefaceProvider(SKData data, int index = 0)
        {
            Typeface = SKTypeface.FromData(data, index);
            FamilyName = Typeface.FamilyName;
        }

        public SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
        {
            var skTypeface = default(SKTypeface);
            var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
            if (fontFamilyNames is { } && fontFamilyNames.Length > 0)
            {
                foreach (var fontFamilyName in fontFamilyNames)
                {
                    if (fontFamily == FamilyName)
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
}
