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

public abstract record SKColorFilter
{
    public static SKColorFilter CreateColorMatrix(float[] matrix) 
        => new ColorMatrixColorFilter(matrix);

    public static SKColorFilter CreateTable(byte[]? tableA, byte[]? tableR, byte[]? tableG, byte[]? tableB) 
        => new TableColorFilter(tableA, tableB, tableG, tableR);

    public static SKColorFilter CreateBlendMode(SKColor c, SKBlendMode mode) 
        => new BlendModeColorFilter(c, mode);

    public static SKColorFilter CreateLumaColor() 
        => new LumaColorColorFilter();
}

public record BlendModeColorFilter(SKColor Color, SKBlendMode Mode) : SKColorFilter;

public record ColorMatrixColorFilter(float[]? Matrix) : SKColorFilter;

public record LumaColorColorFilter : SKColorFilter;

public record TableColorFilter(byte[]? TableA, byte[]? TableR, byte[]? TableG, byte[]? TableB) : SKColorFilter;
