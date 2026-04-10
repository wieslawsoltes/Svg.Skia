// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.IO;
using System.Linq;

namespace Svg.Skia.TypefaceProviders;

public sealed class FontManagerTypefaceProvider : ITypefaceProvider
{
    public static readonly char[] s_fontFamilyTrim = { '\'' };

    private static bool IsGenericFamilyName(string familyName)
    {
        return familyName.Equals("serif", StringComparison.OrdinalIgnoreCase) ||
               familyName.Equals("sans-serif", StringComparison.OrdinalIgnoreCase) ||
               familyName.Equals("monospace", StringComparison.OrdinalIgnoreCase) ||
               familyName.Equals("cursive", StringComparison.OrdinalIgnoreCase) ||
               familyName.Equals("fantasy", StringComparison.OrdinalIgnoreCase);
    }

    public SkiaSharp.SKFontManager FontManager { get; set; }

    public FontManagerTypefaceProvider()
    {
        FontManager = SkiaSharp.SKFontManager.Default;
    }

    public SkiaSharp.SKTypeface CreateTypeface(Stream stream, int index = 0)
    {
        return FontManager.CreateTypeface(stream, index);
    }

    public SkiaSharp.SKTypeface CreateTypeface(SkiaSharp.SKStreamAsset stream, int index = 0)
    {
        return FontManager.CreateTypeface(stream, index);
    }

    public SkiaSharp.SKTypeface CreateTypeface(string path, int index = 0)
    {
        return FontManager.CreateTypeface(path, index);
    }

    public SkiaSharp.SKTypeface CreateTypeface(SkiaSharp.SKData data, int index = 0)
    {
        return FontManager.CreateTypeface(data, index);
    }

    public SkiaSharp.SKTypeface? FromFamilyName(string fontFamily, SkiaSharp.SKFontStyleWeight fontWeight, SkiaSharp.SKFontStyleWidth fontWidth, SkiaSharp.SKFontStyleSlant fontStyle)
    {
        var skTypeface = default(SkiaSharp.SKTypeface);
        var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
        if (fontFamilyNames is { } && fontFamilyNames.Length > 0)
        {
            var defaultName = SkiaSharp.SKTypeface.Default.FamilyName;
            var skFontManager = FontManager;
            var skFontStyle = new SkiaSharp.SKFontStyle(fontWeight, fontWidth, fontStyle);

            foreach (var fontFamilyName in fontFamilyNames)
            {
                var skFontStyleSet = skFontManager.GetFontStyles(fontFamilyName);
                if (skFontStyleSet.Count > 0)
                {
                    skTypeface = skFontManager.MatchFamily(fontFamilyName, skFontStyle);
                    if (skTypeface is { })
                    {
                        var requestedExplicitDefault = defaultName.Equals(fontFamilyName, StringComparison.OrdinalIgnoreCase);
                        var resolvedRequestedFamily = skTypeface.FamilyName.Equals(fontFamilyName, StringComparison.OrdinalIgnoreCase);
                        var resolvedExplicitDefault = defaultName.Equals(skTypeface.FamilyName, StringComparison.OrdinalIgnoreCase);
                        var requestedGenericFamily = IsGenericFamilyName(fontFamilyName);
                        if (!resolvedRequestedFamily &&
                            !(requestedExplicitDefault && resolvedExplicitDefault) &&
                            !(requestedGenericFamily && !resolvedExplicitDefault))
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
