// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public readonly struct SKPoint3
{
    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public static readonly SKPoint3 Empty;

    public readonly bool IsEmpty => X == default && Y == default && Z == default;

    public SKPoint3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override string ToString()
        => FormattableString.Invariant($"{X}, {Y}, {Z}");
}
