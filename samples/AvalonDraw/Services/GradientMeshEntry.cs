using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShimSkiaSharp;
using Svg;
using Svg.Model;

namespace AvalonDraw.Services;

public class GradientMeshEntry : PropertyEntry
{
    public GradientMesh Mesh { get; }

    public GradientMeshEntry(GradientMesh mesh)
        : base("Mesh", string.Empty, (_, __) => { })
    {
        Mesh = mesh;
        Value = Serialize(mesh);
    }

    public override void Apply(object target)
    {
        if (target is SvgGradientServer grad)
        {
            grad.CustomAttributes["gradient-mesh"] = Serialize(Mesh);
        }
    }

    private static string Serialize(GradientMesh mesh)
    {
        var parts = new List<string>(mesh.Points.Count);
        parts.AddRange(mesh.Points.Select(p =>
            FormattableString.Invariant($"{p.Position.X},{p.Position.Y},{p.Color}")));
        return string.Join(';', parts);
    }

    public static GradientMesh Parse(string text)
    {
        var mesh = new GradientMesh();
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var items = part.Split(',');
            if (items.Length != 3)
                continue;
            if (float.TryParse(items[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(items[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                mesh.Points.Add(new GradientMeshPoint(new SKPoint(x, y), items[2]));
            }
        }
        return mesh;
    }
}
