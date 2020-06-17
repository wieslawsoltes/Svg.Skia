using System;
using System.Linq;
using SkiaSharp;

namespace Svg.Skia
{
    public sealed class DefaultTypefaceProvider : ITypefaceProvider
    {
        public static char[] s_fontFamilyTrim = new char[] { '\'' };

        public SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
        {
            var skTypeface = default(SKTypeface);
            var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
            if (fontFamilyNames != null && fontFamilyNames.Length > 0)
            {
                var defaultName = SKTypeface.Default.FamilyName;

                foreach (var fontFamilyName in fontFamilyNames)
                {
                    skTypeface = SKTypeface.FromFamilyName(fontFamilyName, fontWeight, fontWidth, fontStyle);
                    if (skTypeface != null)
                    {
                        if (!skTypeface.FamilyName.Equals(fontFamilyName, StringComparison.Ordinal)
                            && defaultName.Equals(skTypeface.FamilyName, StringComparison.Ordinal))
                        {
                            skTypeface.Dispose();
                            continue;
                        }
                        break;
                    }
                }
            }
            return skTypeface;
        }
    }
}
