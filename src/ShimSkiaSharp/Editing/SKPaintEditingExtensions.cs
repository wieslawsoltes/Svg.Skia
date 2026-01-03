// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp.Editing;

public static class SKPaintEditingExtensions
{
    public static void ApplyColorTransform(this SKPaint paint, Func<SKColor, SKColor> transform)
    {
        if (paint is null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        if (transform is null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        if (paint.Color is { } color)
        {
            paint.Color = transform(color);
        }
    }

    public static void ApplyShaderTransform(this SKPaint paint, Func<SKShader, SKShader> transform)
    {
        if (paint is null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        if (transform is null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        if (paint.Shader is { } shader)
        {
            paint.Shader = transform(shader);
        }
    }
}
