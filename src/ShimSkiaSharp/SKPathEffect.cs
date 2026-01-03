// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public abstract record SKPathEffect : IDeepCloneable<SKPathEffect>
{
    public static SKPathEffect CreateDash(float[] intervals, float phase)
        => new DashPathEffect(intervals, phase);

    public SKPathEffect DeepClone() => DeepClone(new CloneContext());

    internal SKPathEffect DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKPathEffect existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            var clone = this switch
            {
                DashPathEffect dashPathEffect => new DashPathEffect(CloneHelpers.CloneArray(dashPathEffect.Intervals, context), dashPathEffect.Phase),
                _ => throw new NotSupportedException($"Unsupported {nameof(SKPathEffect)} type: {GetType().Name}.")
            };

            context.Add(this, clone);
            return clone;
        }
        finally
        {
            context.Exit(this);
        }
    }
}

public record DashPathEffect(float[]? Intervals, float Phase) : SKPathEffect;
