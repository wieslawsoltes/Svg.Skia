// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
namespace ShimSkiaSharp;

public abstract record SKPathEffect
{
    public static SKPathEffect CreateDash(float[] intervals, float phase)
        => new DashPathEffect(intervals, phase);
}

public record DashPathEffect(float[]? Intervals, float Phase) : SKPathEffect;
