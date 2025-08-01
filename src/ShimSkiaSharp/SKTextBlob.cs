﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
namespace ShimSkiaSharp;

public sealed class SKTextBlob
{
    public string? Text { get; private set; }
    public SKPoint[]? Points { get; private set; }

    private SKTextBlob()
    {
    }

    public static SKTextBlob CreatePositioned(string? text, SKPoint[]? points) 
        => new() {Text = text, Points = points};
}
