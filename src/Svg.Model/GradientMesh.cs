// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Globalization;
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

    public static GradientMesh Parse(string text)
    {
        var mesh = new GradientMesh();
        if (string.IsNullOrWhiteSpace(text))
            return mesh;

        foreach (var part in text.Split(';'))
        {
            var items = part.Split(',');
            if (items.Length != 6)
                continue;

            if (float.TryParse(items[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(items[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                byte.TryParse(items[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse(items[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse(items[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) &&
                byte.TryParse(items[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a))
            {
                mesh.Points.Add(new GradientMeshPoint(new SKPoint(x, y), new SKColor(r, g, b, a)));
            }
        }

        return mesh;
    }

    public override string ToString()
    {
        var parts = new List<string>(Points.Count);
        foreach (var p in Points)
        {
            parts.Add(FormattableString.Invariant($"{p.Position.X},{p.Position.Y},{p.Color.Red},{p.Color.Green},{p.Color.Blue},{p.Color.Alpha}"));
        }
        return string.Join(";", parts);
    }
}

/// <summary>
/// Defines a single mesh point with position and color.
/// </summary>
public sealed record GradientMeshPoint(SKPoint Position, SKColor Color);
