// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Linq;

namespace Svg.Skia.TypefaceProviders;

public sealed class DefaultTypefaceProvider : ITypefaceProvider
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
        return skTypeface;
    }
}
