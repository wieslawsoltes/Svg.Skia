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

public struct SKRect
{
    public float Left { get; set; }

    public float Top { get; set; }

    public float Right { get; set; }

    public float Bottom { get; set; }

    public SKPoint TopLeft => new(Left, Top);

    public SKPoint TopRight => new(Right, Top);

    public SKPoint BottomLeft => new(Left, Bottom);

    public SKPoint BottomRight => new(Right, Bottom);

    public static readonly SKRect Empty = default;

    public readonly bool IsEmpty => Left == default && Top == default && Right == default && Bottom == default;

    public readonly float Width => Right - Left;

    public readonly float Height => Bottom - Top;

    public readonly SKSize Size => new(Width, Height);

    public readonly SKPoint Location => new(Left, Top);

    public SKRect(float left, float top, float right, float bottom)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }

    public static SKRect Create(float x, float y, float width, float height)
    {
        return new()
        {
            Left = x,
            Top = y,
            Right = x + width,
            Bottom = y + height
        };
    }

    public static SKRect Create(SKSize size)
    {
        return Create(0, 0, size.Width, size.Height);
    }

    public bool Contains(SKPoint p)
    {
        return p.X >= Left && p.X <= Left + Width &&
               p.Y >= Top && p.Y <= Top + Height;
    }

    public bool Contains(SKRect r)
    {
        return Contains(r.TopLeft) && Contains(r.BottomRight);
    }

    public static SKRect Union(SKRect a, SKRect b)
    {
        return new(
            Math.Min(a.Left, b.Left),
            Math.Min(a.Top, b.Top),
            Math.Max(a.Right, b.Right),
            Math.Max(a.Bottom, b.Bottom));
    }

    public override string ToString() 
        => FormattableString.Invariant($"{Left}, {Top}, {Width}, {Height}");
}
