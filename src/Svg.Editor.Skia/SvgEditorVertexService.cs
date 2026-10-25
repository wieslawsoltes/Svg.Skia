using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>Edits SVG polygon/polyline vertices without replacing their element identity or attributes.</summary>
public static class SvgEditorVertexService
{
    public static IReadOnlyList<SKPoint> GetVertices(SvgPolygon polygon)
    {
        if (polygon is null)
            throw new ArgumentNullException(nameof(polygon));
        var points = polygon.Points;
        if (points is null || points.Count < 2)
            return Array.Empty<SKPoint>();
        var result = new SKPoint[points.Count / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = new SKPoint(points[i * 2].Value, points[i * 2 + 1].Value);
        return result;
    }

    public static bool MoveVertex(SvgPolygon polygon, int index, SKPoint point)
    {
        ValidatePoint(point);
        var points = polygon?.Points ?? throw new ArgumentNullException(nameof(polygon));
        if (index < 0 || index >= points.Count / 2 || (points.Count & 1) != 0)
            return false;
        var next = Clone(points);
        next[index * 2] = new SvgUnit(points[index * 2].Type, point.X);
        next[index * 2 + 1] = new SvgUnit(points[index * 2 + 1].Type, point.Y);
        polygon.Points = next;
        return true;
    }

    public static bool RemoveVertex(SvgPolygon polygon, int index)
    {
        var points = polygon?.Points ?? throw new ArgumentNullException(nameof(polygon));
        var count = points.Count / 2;
        var minimum = polygon is SvgPolyline ? 2 : 3;
        if (index < 0 || index >= count || count <= minimum || (points.Count & 1) != 0)
            return false;
        var next = Clone(points);
        next.RemoveAt(index * 2 + 1);
        next.RemoveAt(index * 2);
        polygon.Points = next;
        return true;
    }

    /// <summary>Inserts a vertex before the given index; an index equal to the count appends.</summary>
    public static bool InsertVertex(SvgPolygon polygon, int index, SKPoint point)
    {
        ValidatePoint(point);
        var points = polygon?.Points ?? throw new ArgumentNullException(nameof(polygon));
        if (index < 0 || index > points.Count / 2 || (points.Count & 1) != 0)
            return false;
        var next = Clone(points);
        next.Insert(index * 2, new SvgUnit(SvgUnitType.User, point.X));
        next.Insert(index * 2 + 1, new SvgUnit(SvgUnitType.User, point.Y));
        polygon.Points = next;
        return true;
    }

    private static SvgPointCollection Clone(SvgPointCollection points)
    {
        var result = new SvgPointCollection();
        foreach (var point in points)
            result.Add(point);
        return result;
    }

    private static void ValidatePoint(SKPoint point)
    {
        if (float.IsNaN(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.X) || float.IsInfinity(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point), "Vertex coordinates must be finite.");
    }
}
