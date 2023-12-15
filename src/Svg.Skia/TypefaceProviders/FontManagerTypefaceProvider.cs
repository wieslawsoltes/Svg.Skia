/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.IO;
using System.Linq;

namespace Svg.Skia.TypefaceProviders;

public sealed class FontManagerTypefaceProvider : ITypefaceProvider
{
    public static readonly char[] s_fontFamilyTrim = { '\'' };

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
