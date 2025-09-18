// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
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
