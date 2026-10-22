using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>A rectangular network of cubic Coons patches with shared boundary tangents.</summary>
public sealed class SvgGradientMesh
{
    public int Rows { get; set; } = 2;
    public int Columns { get; set; } = 2;
    public int Subdivisions { get; set; } = 24;
    public List<SvgMeshNode> Nodes { get; } = new();
}

/// <summary>Position, horizontal/vertical derivatives, and a premultiplied interpolation color.</summary>
public sealed class SvgMeshNode
{
    public SKPoint Position { get; set; }
    public SKPoint Horizontal { get; set; }
    public SKPoint Vertical { get; set; }
    public SKColor Color { get; set; } = SKColors.Coral;
}

public readonly struct SvgMeshTriangle
{
    public SvgMeshTriangle(SKPoint a, SKPoint b, SKPoint c, SKColor color)
    { A = a; B = b; C = c; Color = color; }
    public SKPoint A { get; }
    public SKPoint B { get; }
    public SKPoint C { get; }
    public SKColor Color { get; }
}

/// <summary>
/// Authors cubic Coons mesh geometry as a portable SVG polygon group, retaining the editable
/// control network in metadata. Geometry follows the W3C Coons construction:
/// https://www.w3.org/TR/2016/CR-SVG2-20160915/pservers.html#MeshGradients
/// The saved representation uses SVG 1.1 polygons, so it does not require a mesh paint server.
/// </summary>
public static class SvgGradientMeshService
{
    public const string MeshAttribute = "data-unodraw-mesh";
    public const string FingerprintAttribute = "data-unodraw-mesh-fingerprint";
    public const string CellAttribute = "data-unodraw-mesh-cell";
    private const int MaximumTriangles = 32768;

    public static SvgGradientMesh CreateGrid(SKRect bounds, int rows = 2, int columns = 2)
    {
        if (!Finite(bounds.Left) || !Finite(bounds.Top) || !Finite(bounds.Right) || !Finite(bounds.Bottom)
            || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("The mesh needs a finite, positive rectangle.", nameof(bounds));
        if (rows is < 2 or > 16 || columns is < 2 or > 16)
            throw new ArgumentOutOfRangeException(nameof(rows), "A mesh has between 2 and 16 nodes in each direction.");
        var mesh = new SvgGradientMesh
        {
            Rows = rows,
            Columns = columns,
            Subdivisions = Math.Min(24, (int)Math.Sqrt(MaximumTriangles / (2d * (rows - 1) * (columns - 1))))
        };
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var u = column / (float)(columns - 1);
                var v = row / (float)(rows - 1);
                mesh.Nodes.Add(new SvgMeshNode
                {
                    Position = new SKPoint(bounds.Left + u * bounds.Width, bounds.Top + v * bounds.Height),
                    Horizontal = new SKPoint(bounds.Width / (columns - 1), 0),
                    Vertical = new SKPoint(0, bounds.Height / (rows - 1)),
                    Color = InterpolateColor(SKColor.Parse("#7061DC"), SKColor.Parse("#FFB184"),
                        SKColor.Parse("#477F78"), SKColor.Parse("#B7DDD1"), u, v)
                });
            }
        return mesh;
    }

    public static SvgGroup Create(SvgGradientMesh mesh, string? id = null)
    {
        var group = new SvgGroup { ID = id };
        Apply(group, mesh);
        return group;
    }

    /// <summary>Stages all geometry before touching the target. Wrap in Execute for undo.</summary>
    public static void Apply(SvgGroup group, SvgGradientMesh mesh)
    {
        if (group is null)
            throw new ArgumentNullException(nameof(group));
        if (SvgEditorController.IsEditLocked(group))
            throw new InvalidOperationException("Unlock the mesh or its layer before editing it.");
        if (group.CustomAttributes.ContainsKey(MeshAttribute))
            GetMesh(group); // Do not overwrite independently edited generated polygons.
        else if (group.Children.OfType<SvgVisualElement>().Any())
            throw new InvalidOperationException("Create a new mesh group instead of replacing existing artwork.");
        var triangles = Tessellate(mesh);
        var polygons = triangles.Select((triangle, index) =>
        {
            var polygon = new SvgPolygon
            {
                Points = new SvgPointCollection { triangle.A.X, triangle.A.Y, triangle.B.X, triangle.B.Y, triangle.C.X, triangle.C.Y },
                Fill = new SvgColourServer(System.Drawing.Color.FromArgb(triangle.Color.Red, triangle.Color.Green, triangle.Color.Blue)),
                FillOpacity = triangle.Color.Alpha / 255f,
                Stroke = SvgPaintServer.None,
                ShapeRendering = SvgShapeRendering.CrispEdges
            };
            polygon.CustomAttributes[CellAttribute] = index.ToString(CultureInfo.InvariantCulture);
            return polygon;
        }).ToArray();
        var metadata = Convert.ToBase64String(Encoding.UTF8.GetBytes(Serialize(mesh)));
        foreach (var child in group.Children.Where(child => child.CustomAttributes.ContainsKey(CellAttribute)).ToArray())
            group.Children.Remove(child);
        foreach (var polygon in polygons)
            group.Children.Add(polygon);
        group.CustomAttributes[MeshAttribute] = metadata;
        group.CustomAttributes[FingerprintAttribute] = Fingerprint(group);
    }

    public static SvgGradientMesh GetMesh(SvgGroup group)
    {
        if (group is null)
            throw new ArgumentNullException(nameof(group));
        if (!group.CustomAttributes.TryGetValue(MeshAttribute, out var data))
            throw new ArgumentException("The selected group is not an editable gradient mesh.", nameof(group));
        if (!group.CustomAttributes.TryGetValue(FingerprintAttribute, out var fingerprint)
            || !string.Equals(fingerprint, Fingerprint(group), StringComparison.Ordinal))
            throw new InvalidOperationException("The mesh polygons were edited independently. Undo those edits before changing the control network.");
        if (data.Length > 1400000)
            throw new FormatException("The mesh metadata exceeds its supported size.");
        return Deserialize(Encoding.UTF8.GetString(Convert.FromBase64String(data)));
    }

    public static IReadOnlyList<SvgMeshTriangle> Tessellate(SvgGradientMesh mesh)
    {
        Validate(mesh);
        var result = new List<SvgMeshTriangle>((mesh.Rows - 1) * (mesh.Columns - 1) * mesh.Subdivisions * mesh.Subdivisions * 2);
        var steps = mesh.Subdivisions;
        for (var row = 0; row < mesh.Rows - 1; row++)
            for (var column = 0; column < mesh.Columns - 1; column++)
            {
                var a = mesh.Nodes[row * mesh.Columns + column];
                var b = mesh.Nodes[row * mesh.Columns + column + 1];
                var c = mesh.Nodes[(row + 1) * mesh.Columns + column];
                var d = mesh.Nodes[(row + 1) * mesh.Columns + column + 1];
                for (var y = 0; y < steps; y++)
                    for (var x = 0; x < steps; x++)
                    {
                        var u0 = x / (float)steps;
                        var u1 = (x + 1) / (float)steps;
                        var v0 = y / (float)steps;
                        var v1 = (y + 1) / (float)steps;
                        var p00 = Evaluate(a, b, c, d, u0, v0);
                        var p10 = Evaluate(a, b, c, d, u1, v0);
                        var p01 = Evaluate(a, b, c, d, u0, v1);
                        var p11 = Evaluate(a, b, c, d, u1, v1);
                        Add(p00, p10, p11, (u0 + 2 * u1) / 3, (2 * v0 + v1) / 3);
                        Add(p00, p11, p01, (2 * u0 + u1) / 3, (v0 + 2 * v1) / 3);
                    }
                void Add(SKPoint p, SKPoint q, SKPoint r, float u, float v)
                {
                    if (!PointFinite(p) || !PointFinite(q) || !PointFinite(r))
                        throw new ArgumentException("The mesh produces coordinates outside its supported range.", nameof(mesh));
                    result.Add(new SvgMeshTriangle(p, q, r, InterpolateColor(a.Color, b.Color, c.Color, d.Color, u, v)));
                }
            }
        return result;
    }

    public static string Serialize(SvgGradientMesh mesh)
    {
        Validate(mesh);
        return new XElement("unodraw-mesh", new XAttribute("version", 1), new XAttribute("rows", mesh.Rows),
            new XAttribute("columns", mesh.Columns), new XAttribute("subdivisions", mesh.Subdivisions),
            mesh.Nodes.Select(node => new XElement("node", new XAttribute("x", node.Position.X), new XAttribute("y", node.Position.Y),
                new XAttribute("ux", node.Horizontal.X), new XAttribute("uy", node.Horizontal.Y),
                new XAttribute("vx", node.Vertical.X), new XAttribute("vy", node.Vertical.Y),
                new XAttribute("color", $"#{node.Color.Red:X2}{node.Color.Green:X2}{node.Color.Blue:X2}{node.Color.Alpha:X2}"))))
            .ToString(SaveOptions.DisableFormatting);
    }

    public static SvgGradientMesh Deserialize(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml ?? throw new ArgumentNullException(nameof(xml))),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1000000 });
        var root = XElement.Load(reader);
        if (root.Name != "unodraw-mesh" || (int?)root.Attribute("version") != 1)
            throw new FormatException("This mesh format or version is not supported.");
        var mesh = new SvgGradientMesh
        {
            Rows = (int?)root.Attribute("rows") ?? 0,
            Columns = (int?)root.Attribute("columns") ?? 0,
            Subdivisions = (int?)root.Attribute("subdivisions") ?? 0
        };
        foreach (var element in root.Elements("node"))
        {
            if (mesh.Nodes.Count == 256)
                throw new FormatException("The mesh has too many nodes.");
            var color = (string?)element.Attribute("color") ?? string.Empty;
            if (color.Length != 9 || color[0] != '#' || !uint.TryParse(color.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba))
                throw new FormatException("Each mesh color must be #RRGGBBAA.");
            mesh.Nodes.Add(new SvgMeshNode
            {
                Position = new SKPoint(Required("x"), Required("y")),
                Horizontal = new SKPoint(Required("ux"), Required("uy")),
                Vertical = new SKPoint(Required("vx"), Required("vy")),
                Color = new SKColor((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba)
            });
            float Required(string name) => (float?)element.Attribute(name) ?? throw new FormatException("Missing mesh coordinate: " + name);
        }
        Validate(mesh);
        return mesh;
    }

    private static void Validate(SvgGradientMesh mesh)
    {
        if (mesh is null)
            throw new ArgumentNullException(nameof(mesh));
        if (mesh.Rows is < 2 or > 16 || mesh.Columns is < 2 or > 16 || mesh.Nodes.Count != mesh.Rows * mesh.Columns)
            throw new ArgumentException("A mesh needs a complete rectangular grid of 2–16 nodes in each direction.", nameof(mesh));
        if (mesh.Subdivisions is < 2 or > 128 || (long)(mesh.Rows - 1) * (mesh.Columns - 1) * mesh.Subdivisions * mesh.Subdivisions * 2 > MaximumTriangles)
            throw new ArgumentException("Choose a mesh quality and grid size that generate at most 32,768 triangles.", nameof(mesh));
        if (mesh.Nodes.Any(node => node is null || !PointFinite(node.Position) || !PointFinite(node.Horizontal) || !PointFinite(node.Vertical)))
            throw new ArgumentException("Mesh positions and tangents must be finite and within ±10,000,000 SVG units.", nameof(mesh));
    }

    private static SKPoint Evaluate(SvgMeshNode a, SvgMeshNode b, SvgMeshNode c, SvgMeshNode d, float u, float v)
    {
        var top = Curve(a.Position, b.Position, a.Horizontal, b.Horizontal, u);
        var bottom = Curve(c.Position, d.Position, c.Horizontal, d.Horizontal, u);
        var left = Curve(a.Position, c.Position, a.Vertical, c.Vertical, v);
        var right = Curve(b.Position, d.Position, b.Vertical, d.Vertical, v);
        var bilinear = Lerp(Lerp(a.Position, b.Position, u), Lerp(c.Position, d.Position, u), v);
        var horizontal = Lerp(top, bottom, v);
        var vertical = Lerp(left, right, u);
        return new SKPoint(horizontal.X + vertical.X - bilinear.X, horizontal.Y + vertical.Y - bilinear.Y);
    }

    private static SKPoint Curve(SKPoint a, SKPoint b, SKPoint ta, SKPoint tb, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        var h0 = 2 * t3 - 3 * t2 + 1;
        var h1 = -2 * t3 + 3 * t2;
        var h2 = t3 - 2 * t2 + t;
        var h3 = t3 - t2;
        return new SKPoint(a.X * h0 + b.X * h1 + ta.X * h2 + tb.X * h3,
            a.Y * h0 + b.Y * h1 + ta.Y * h2 + tb.Y * h3);
    }

    private static SKColor InterpolateColor(SKColor a, SKColor b, SKColor c, SKColor d, float u, float v)
    {
        var wa = (1 - u) * (1 - v) * a.Alpha / 255d;
        var wb = u * (1 - v) * b.Alpha / 255d;
        var wc = (1 - u) * v * c.Alpha / 255d;
        var wd = u * v * d.Alpha / 255d;
        var alpha = wa + wb + wc + wd;
        var red = a.Red * wa + b.Red * wb + c.Red * wc + d.Red * wd;
        var green = a.Green * wa + b.Green * wb + c.Green * wc + d.Green * wd;
        var blue = a.Blue * wa + b.Blue * wb + c.Blue * wc + d.Blue * wd;
        if (alpha <= 0)
            return SKColors.Transparent;
        byte Component(double number) => (byte)Math.Max(0, Math.Min(255, Math.Round(number)));
        return new SKColor(Component(red / alpha), Component(green / alpha), Component(blue / alpha), Component(alpha * 255));
    }

    private static string Fingerprint(SvgGroup group)
    {
        // Geometry/paint fingerprint excludes IDs so collision-safe copy/paste remains editable.
        var data = new StringBuilder();
        foreach (var element in group.Children.Where(child => child is SvgVisualElement))
        {
            if (element is not SvgPolygon polygon || !polygon.CustomAttributes.ContainsKey(CellAttribute))
                throw new InvalidOperationException("The mesh contains independently added artwork. Undo that change before editing its network.");
            var clone = (SvgPolygon)polygon.DeepCopy();
            clone.ID = null;
            // Parsing may canonicalize #FF7F50 to the named color "coral". Compare the
            // identical color value, rather than treating that serializer choice as an edit.
            if (clone.Fill is SvgColourServer color)
                clone.Fill = new SvgColourServer(System.Drawing.Color.FromArgb(color.Colour.A, color.Colour.R, color.Colour.G, color.Colour.B));
            data.Append(clone.GetXML());
        }
        using var hash = SHA256.Create();
        return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(data.ToString())));
    }

    private static SKPoint Lerp(SKPoint a, SKPoint b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool PointFinite(SKPoint p) => Finite(p.X) && Finite(p.Y) && Math.Abs(p.X) <= 10000000 && Math.Abs(p.Y) <= 10000000;
}
