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
using System.Collections.Generic;
using System.IO;

namespace svgc;

internal class ImageSharpAssetLoader : Svg.Model.IAssetLoader
{
    public ShimSkiaSharp.SKImage LoadImage(Stream stream)
    {
        var data = ShimSkiaSharp.SKImage.FromStream(stream);
        using var image = SixLabors.ImageSharp.Image.Load(data);
        return new ShimSkiaSharp.SKImage {Data = data, Width = image.Width, Height = image.Height};
    }

    public List<Svg.Model.TypefaceSpan> FindTypefaces(string? text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        if (text is null || string.IsNullOrEmpty(text))
        {
            return new List<Svg.Model.TypefaceSpan>();
        }

        // TODO:
        // Font fallback and text advancing code should be generated along with canvas commands instead.
        // Otherwise, some package reference hacking may be needed.
        return new List<Svg.Model.TypefaceSpan>
        {
            new(text, text.Length * paintPreferredTypeface.TextSize, paintPreferredTypeface.Typeface)
        };
    }
}
