// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model;

/// <summary>
/// Represents a simple gradient mesh consisting of colored points.
/// </summary>
public sealed class GradientMesh
{
    /// <summary>
    /// List of mesh points.
    /// </summary>
    public List<GradientMeshPoint> Points { get; } = new();
}

/// <summary>
/// Defines a single mesh point with position and color.
/// </summary>
public sealed record GradientMeshPoint(SKPoint Position, SKColor Color);
