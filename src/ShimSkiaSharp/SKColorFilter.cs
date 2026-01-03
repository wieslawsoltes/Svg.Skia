// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public abstract record SKColorFilter : IDeepCloneable<SKColorFilter>
{
    public static SKColorFilter CreateColorMatrix(float[] matrix)
        => new ColorMatrixColorFilter(matrix);

    public static SKColorFilter CreateTable(byte[]? tableA, byte[]? tableR, byte[]? tableG, byte[]? tableB)
        => new TableColorFilter(tableA, tableB, tableG, tableR);

    public static SKColorFilter CreateBlendMode(SKColor c, SKBlendMode mode)
        => new BlendModeColorFilter(c, mode);

    public static SKColorFilter CreateLumaColor()
        => new LumaColorColorFilter();

    public SKColorFilter DeepClone()
    {
        return this switch
        {
            BlendModeColorFilter blendModeColorFilter => new BlendModeColorFilter(blendModeColorFilter.Color, blendModeColorFilter.Mode),
            ColorMatrixColorFilter colorMatrixColorFilter => new ColorMatrixColorFilter(CloneHelpers.CloneArray(colorMatrixColorFilter.Matrix)),
            LumaColorColorFilter => new LumaColorColorFilter(),
            TableColorFilter tableColorFilter => new TableColorFilter(CloneHelpers.CloneArray(tableColorFilter.TableA), CloneHelpers.CloneArray(tableColorFilter.TableR), CloneHelpers.CloneArray(tableColorFilter.TableG), CloneHelpers.CloneArray(tableColorFilter.TableB)),
            _ => throw new NotSupportedException($"Unsupported {nameof(SKColorFilter)} type: {GetType().Name}.")
        };
    }
}

public record BlendModeColorFilter(SKColor Color, SKBlendMode Mode) : SKColorFilter;

public record ColorMatrixColorFilter(float[]? Matrix) : SKColorFilter;

public record LumaColorColorFilter : SKColorFilter;

public record TableColorFilter(byte[]? TableA, byte[]? TableR, byte[]? TableG, byte[]? TableB) : SKColorFilter;
