// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public sealed class SKTextBlob : ICloneable, IDeepCloneable<SKTextBlob>
{
    public string? Text { get; private set; }
    public SKPoint[]? Points { get; private set; }

    private SKTextBlob()
    {
    }

    public static SKTextBlob CreatePositioned(string? text, SKPoint[]? points)
        => new() { Text = text, Points = points };

    public SKTextBlob Clone()
    {
        return new SKTextBlob
        {
            Text = Text,
            Points = CloneHelpers.CloneArray(Points)
        };
    }

    public SKTextBlob DeepClone() => Clone();

    object ICloneable.Clone() => Clone();
}
