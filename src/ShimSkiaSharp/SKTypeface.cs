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
namespace ShimSkiaSharp;

public class SKTypeface
{
    public string? FamilyName { get; private set; }
    public SKFontStyleWeight FontWeight { get; private set; }
    public SKFontStyleWidth FontWidth { get; private set; }
    public SKFontStyleSlant FontSlant { get; private set; }

    private SKTypeface()
    {
    }
    
    public static SKTypeface FromFamilyName(
        string familyName,
        SKFontStyleWeight weight,
        SKFontStyleWidth width,
        SKFontStyleSlant slant)
    {
        return new()
        {
            FamilyName = familyName,
            FontWeight = weight,
            FontWidth = width,
            FontSlant = slant
        };
    }
}
