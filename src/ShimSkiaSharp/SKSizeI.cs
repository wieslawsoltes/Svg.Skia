﻿using System;

namespace ShimSkiaSharp;

public readonly struct SKSizeI
{
    public int Width { get; }

    public int Height { get; }

    public static readonly SKSizeI Empty;

    public readonly bool IsEmpty => Width == default && Height == default;

    public SKSizeI(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public override string ToString() 
        => FormattableString.Invariant($"{Width}, {Height}");
}