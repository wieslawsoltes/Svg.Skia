// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Linq;

namespace Svg.Skia.TypefaceProviders;

public sealed class DefaultTypefaceProvider : ITypefaceProvider
{
    public static readonly char[] s_fontFamilyTrim = { '\'' };

    public SkiaSharp.SKTypeface? FromFamilyName(string fontFamily, SkiaSharp.SKFontStyleWeight fontWeight, SkiaSharp.SKFontStyleWidth fontWidth, SkiaSharp.SKFontStyleSlant fontStyle)
    {
        var skTypeface = default(SkiaSharp.SKTypeface);
        var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
        if (fontFamilyNames is { } && fontFamilyNames.Length > 0)
        {
            var defaultName = SkiaSharp.SKTypeface.Default.FamilyName;

            foreach (var fontFamilyName in fontFamilyNames)
            {
                skTypeface = SkiaSharp.SKTypeface.FromFamilyName(fontFamilyName, fontWeight, fontWidth, fontStyle);
                if (skTypeface is { })
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
