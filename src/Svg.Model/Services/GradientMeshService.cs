// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;

namespace Svg.Model.Services;

/// <summary>
/// Provides conversion helpers for <see cref="GradientMesh"/> instances.
/// </summary>
public static class GradientMeshService
{
    /// <summary>
    /// Convert gradient mesh points to a mesh shader if supported. Currently this
    /// implementation falls back to a linear gradient created between the first
    /// and last mesh points when mesh shaders are not available.
    /// </summary>
    public static SKShader? ToShader(GradientMesh mesh)
    {
        if (mesh.Points.Count < 2)
            return null;

        // TODO: use mesh gradient when available in ShimSkiaSharp
        var first = mesh.Points.First();
        var last = mesh.Points.Last();
        return SKShader.CreateLinearGradient(
            first.Position,
            last.Position,
            new[] { (SKColorF)first.ToSKColor(), (SKColorF)last.ToSKColor() },
            SKColorSpace.Srgb,
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp);
    }
}
