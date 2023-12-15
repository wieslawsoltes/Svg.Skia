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

namespace ShimSkiaSharp;

public readonly struct SKPointI
{
    public int X { get; }

    public int Y { get; }

    public static readonly SKPointI Empty;

    public readonly bool IsEmpty => X == default && Y == default;

    public SKPointI(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() 
        => FormattableString.Invariant($"{X}, {Y}");
}
