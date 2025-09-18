// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public readonly struct SKSize
{
    public float Width { get; }

    public float Height { get; }

    public static readonly SKSize Empty;

    public readonly bool IsEmpty => Width == default && Height == default;

    public SKSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public override string ToString()
        => FormattableString.Invariant($"{Width}, {Height}");
}
