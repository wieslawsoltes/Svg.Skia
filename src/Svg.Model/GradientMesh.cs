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
public sealed class GradientMeshPoint
{
    public SKPoint Position { get; set; }
    public string Color { get; set; } = "#000000";

    public GradientMeshPoint() { }

    public GradientMeshPoint(SKPoint position, string color)
    {
        Position = position;
        Color = color;
    }

    public SKColor ToSKColor()
    {
        var hex = Color.TrimStart('#');
        byte a = 255;
        byte r = 0;
        byte g = 0;
        byte b = 0;
        if (hex.Length == 8)
        {
            a = System.Convert.ToByte(hex.Substring(0, 2), 16);
            r = System.Convert.ToByte(hex.Substring(2, 2), 16);
            g = System.Convert.ToByte(hex.Substring(4, 2), 16);
            b = System.Convert.ToByte(hex.Substring(6, 2), 16);
        }
        else if (hex.Length == 6)
        {
            r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            b = System.Convert.ToByte(hex.Substring(4, 2), 16);
        }
        return new SKColor(r, g, b, a);
    }
}
