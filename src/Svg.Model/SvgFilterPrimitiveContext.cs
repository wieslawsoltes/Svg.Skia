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
using Svg.FilterEffects;
using ShimSkiaSharp;

namespace Svg.Model;

internal class SvgFilterPrimitiveContext
{
    public SvgFilterPrimitiveContext(SvgFilterPrimitive svgFilterPrimitive)
    {
        FilterPrimitive = svgFilterPrimitive;
    }

    public SvgFilterPrimitive FilterPrimitive { get; }

    public SKRect Boundaries { get; set; }

    public bool IsXValid { get; set; }

    public bool IsYValid { get; set; }

    public bool IsWidthValid { get; set; }

    public bool IsHeightValid { get; set; }

    public SvgUnit X { get; set; }

    public SvgUnit Y { get; set; }

    public SvgUnit Width { get; set; }

    public SvgUnit Height { get; set; }
}
