// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public readonly struct SKPoint
{
    public float X { get; }

    public float Y { get; }

    public static readonly SKPoint Empty = default;

    public readonly bool IsEmpty => X == default && Y == default;

    public SKPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    public override string ToString()
        => FormattableString.Invariant($"{X}, {Y}");
}
