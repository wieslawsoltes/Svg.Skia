// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public readonly struct SKColor : IEquatable<SKColor>
{
    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }

    public byte Alpha { get; }

    public static readonly SKColor Empty = default;

    public SKColor(byte red, byte green, byte blue, byte alpha)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    public static implicit operator SKColorF(SKColor color)
    {
        return new(
            color.Red * (1 / 255.0f),
            color.Green * (1 / 255.0f),
            color.Blue * (1 / 255.0f),
            color.Alpha * (1 / 255.0f));
    }

    public bool Equals(SKColor other)
    {
        return Red == other.Red &&
               Green == other.Green &&
               Blue == other.Blue &&
               Alpha == other.Alpha;
    }

    public override bool Equals(object? obj)
        => obj is SKColor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Red.GetHashCode();
            hash = (hash * 397) ^ Green.GetHashCode();
            hash = (hash * 397) ^ Blue.GetHashCode();
            hash = (hash * 397) ^ Alpha.GetHashCode();
            return hash;
        }
    }

    public override string ToString()
        => FormattableString.Invariant($"{Red}, {Green}, {Blue}, {Alpha}");
}
