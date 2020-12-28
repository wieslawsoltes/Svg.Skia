using System;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace Svg.Skia.TypefaceProviders
{
    public sealed class FontManagerTypefacerovider : ITypefaceProvider
    {
        public static readonly char[] s_fontFamilyTrim = { '\'' };

        public SKFontManager FontManager { get; set; }

        public FontManagerTypefacerovider()
        {
            FontManager = SKFontManager.Default;
        }

        public SKTypeface CreateTypeface(Stream stream, int index = 0)
        {
            return FontManager.CreateTypeface(stream, index);
        }

        public SKTypeface CreateTypeface(SKStreamAsset stream, int index = 0)
        {
            return FontManager.CreateTypeface(stream, index);
        }

        public SKTypeface CreateTypeface(string path, int index = 0)
        {
            return FontManager.CreateTypeface(path, index);
        }

        public SKTypeface CreateTypeface(SKData data, int index = 0)
        {
            return FontManager.CreateTypeface(data, index);
        }

        public SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
        {
            var skTypeface = default(SKTypeface);
            var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
            if (fontFamilyNames != null && fontFamilyNames.Length > 0)
            {
                var defaultName = SKTypeface.Default.FamilyName;
                var skFontManager = FontManager;
                var skFontStyle = new SKFontStyle(fontWeight, fontWidth, fontStyle);

                foreach (var fontFamilyName in fontFamilyNames)
                {
                    var skFontStyleSet = skFontManager.GetFontStyles(fontFamilyName);
                    if (skFontStyleSet.Count > 0)
                    {
                        skTypeface = skFontManager.MatchFamily(fontFamilyName, skFontStyle);
                        if (skTypeface != null)
                        {
                            if (!defaultName.Equals(fontFamilyName, StringComparison.Ordinal)
                                && defaultName.Equals(skTypeface.FamilyName, StringComparison.Ordinal))
                            {
                                skTypeface.Dispose();
                                skTypeface = null;
                                continue;
                            }
                            break;
                        }
                    }
                }
            }
            return skTypeface;
        }
    }
}
