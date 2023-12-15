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
