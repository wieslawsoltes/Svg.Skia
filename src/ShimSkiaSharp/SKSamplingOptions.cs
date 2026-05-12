// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace ShimSkiaSharp;

public enum SKFilterMode
{
    Nearest,
    Linear
}

public enum SKMipmapMode
{
    None,
    Nearest,
    Linear
}

public readonly struct SKCubicResampler
{
    public static readonly SKCubicResampler Mitchell = new(1f / 3f, 1f / 3f);
    public static readonly SKCubicResampler CatmullRom = new(0f, 1f / 2f);

    public SKCubicResampler(float b, float c)
    {
        B = b;
        C = c;
    }

    public float B { get; }

    public float C { get; }
}

public readonly struct SKSamplingOptions
{
    public static readonly SKSamplingOptions Default = new();

    public SKSamplingOptions(SKFilterMode filter, SKMipmapMode mipmap = SKMipmapMode.None)
    {
        Filter = filter;
        Mipmap = mipmap;
        Cubic = default;
        UseCubic = false;
    }

    public SKSamplingOptions(SKCubicResampler cubic)
    {
        Filter = default;
        Mipmap = default;
        Cubic = cubic;
        UseCubic = true;
    }

    public SKFilterMode Filter { get; }

    public SKMipmapMode Mipmap { get; }

    public SKCubicResampler Cubic { get; }

    public bool UseCubic { get; }
}
