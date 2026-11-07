using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>An infinite, nonprinting construction line in the caller's coordinate system.</summary>
public readonly struct SvgEditorGuide
{
    public SvgEditorGuide(string id, SKPoint origin, SKPoint direction)
    { Id = id; Origin = origin; Direction = direction; }
    public string Id { get; }
    public SKPoint Origin { get; }
    public SKPoint Direction { get; }
}

/// <summary>SVG metadata for guides; coordinates are stored in root user units.</summary>
public static class SvgEditorGuideService
{
    private const string Attribute = "data-unodraw-guides";
    public const int MaximumGuides = 512;

    public static IReadOnlyList<SvgEditorGuide> Read(SvgDocument? document)
    {
        var result = new List<SvgEditorGuide>();
        if (document is null || !document.CustomAttributes.TryGetValue(Attribute, out var data)
            || data.Length > 100000 || !data.StartsWith("1;", StringComparison.Ordinal))
            return result;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in data.Substring(2).Split(';'))
        {
            var fields = entry.Split(',');
            if (fields.Length != 5 || !Guid.TryParseExact(fields[0], "N", out _) || !ids.Add(fields[0]))
                continue;
            var values = new float[4];
            var valid = true;
            for (var index = 0; index < values.Length; index++)
                valid &= float.TryParse(fields[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index]) && Finite(values[index]);
            if (!valid)
                continue;
            var direction = new SKPoint(values[2], values[3]);
            if (!TryNormalize(direction, out direction))
                continue;
            result.Add(new SvgEditorGuide(fields[0], new SKPoint(values[0], values[1]), direction));
            if (result.Count == MaximumGuides)
                break;
        }
        return result;
    }

    public static void Write(SvgDocument document, IReadOnlyList<SvgEditorGuide> guides)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (guides is null)
            throw new ArgumentNullException(nameof(guides));
        if (guides.Count > MaximumGuides)
            throw new ArgumentException("The document has reached the guide limit.", nameof(guides));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<string>();
        foreach (var guide in guides)
        {
            if (!Guid.TryParseExact(guide.Id, "N", out _) || !ids.Add(guide.Id)
                || !Finite(guide.Origin.X) || !Finite(guide.Origin.Y) || !TryNormalize(guide.Direction, out var direction))
                throw new ArgumentException("Guides need unique identifiers and finite, nonzero geometry.", nameof(guides));
            string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
            entries.Add(string.Join(",", guide.Id, Number(guide.Origin.X), Number(guide.Origin.Y), Number(direction.X), Number(direction.Y)));
        }
        // Validate the complete collection before changing the document.
        if (entries.Count == 0)
            document.CustomAttributes.Remove(Attribute);
        else
            document.CustomAttributes[Attribute] = "1;" + string.Join(";", entries);
    }

    public static bool TryNormalize(SKPoint vector, out SKPoint normalized)
    {
        var length = Math.Sqrt((double)vector.X * vector.X + (double)vector.Y * vector.Y);
        if (!Finite(vector.X) || !Finite(vector.Y) || length < 1e-12)
        { normalized = default; return false; }
        normalized = new SKPoint((float)(vector.X / length), (float)(vector.Y / length));
        return true;
    }

    public static SKPoint Project(SKPoint point, SvgEditorGuide guide)
    {
        if (!TryNormalize(guide.Direction, out var direction))
            return point;
        var offset = (double)(point.X - guide.Origin.X) * direction.X + (double)(point.Y - guide.Origin.Y) * direction.Y;
        return new SKPoint((float)(guide.Origin.X + offset * direction.X), (float)(guide.Origin.Y + offset * direction.Y));
    }

    /// <summary>Snaps to the nearest line or to a nearby intersection without zoom-dependent drift.</summary>
    public static SKPoint Snap(SKPoint point, float tolerance, IReadOnlyList<SvgEditorGuide> guides,
        out string? firstGuide, out string? secondGuide)
    {
        if (!Finite(point.X) || !Finite(point.Y) || !Finite(tolerance) || tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(point));
        if (guides is null)
            throw new ArgumentNullException(nameof(guides));
        firstGuide = secondGuide = null;
        var nearest = tolerance;
        var first = -1;
        var result = point;
        for (var index = 0; index < guides.Count; index++)
        {
            if (!Finite(guides[index].Origin.X) || !Finite(guides[index].Origin.Y)
                || !TryNormalize(guides[index].Direction, out _))
                continue;
            var projection = Project(point, guides[index]);
            var distance = SKPoint.Distance(point, projection);
            if (!Finite(distance) || distance > nearest)
                continue;
            nearest = distance;
            first = index;
            result = projection;
            firstGuide = guides[index].Id;
        }
        if (first < 0)
            return point;
        var line = guides[first];
        var intersectionDistance = tolerance * Math.Sqrt(2);
        for (var index = 0; index < guides.Count; index++)
        {
            if (index == first || SKPoint.Distance(point, Project(point, guides[index])) > tolerance)
                continue;
            var other = guides[index];
            var determinant = (double)line.Direction.X * other.Direction.Y - (double)line.Direction.Y * other.Direction.X;
            if (Math.Abs(determinant) < 1e-6)
                continue;
            var dx = (double)other.Origin.X - line.Origin.X;
            var dy = (double)other.Origin.Y - line.Origin.Y;
            var t = (dx * other.Direction.Y - dy * other.Direction.X) / determinant;
            var intersection = new SKPoint((float)(line.Origin.X + t * line.Direction.X), (float)(line.Origin.Y + t * line.Direction.Y));
            var distance = SKPoint.Distance(point, intersection);
            if (!Finite(distance) || distance > intersectionDistance)
                continue;
            intersectionDistance = distance;
            result = intersection;
            secondGuide = other.Id;
        }
        return result;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed partial class SvgEditorController
{
    public IReadOnlyList<SvgEditorGuide> GetGuides()
    {
        ThrowIfDisposed();
        var transform = GuideRootTransform();
        return SvgEditorGuideService.Read(Document).Select(guide => TransformGuide(guide, transform)).ToArray();
    }

    public string AddGuide(SKPoint pictureOrigin, SKPoint pictureDirection)
    {
        EnsureGuidesEditable();
        var guide = new SvgEditorGuide(Guid.NewGuid().ToString("N"), pictureOrigin, pictureDirection);
        var local = GuideInRootCoordinates(guide);
        var guides = SvgEditorGuideService.Read(Document).ToList();
        guides.Add(local);
        Execute("Create guide", () => SvgEditorGuideService.Write(Document!, guides));
        return guide.Id;
    }

    public void MoveGuide(string id, SKPoint pictureOrigin)
    {
        EnsureGuidesEditable();
        var guides = SvgEditorGuideService.Read(Document).ToList();
        var index = guides.FindIndex(guide => guide.Id == id);
        if (index < 0)
            return;
        var picture = TransformGuide(guides[index], GuideRootTransform());
        guides[index] = GuideInRootCoordinates(new SvgEditorGuide(id, pictureOrigin, picture.Direction));
        Execute("Move guide", () => SvgEditorGuideService.Write(Document!, guides));
    }

    public void RemoveGuide(string id)
    {
        EnsureGuidesEditable();
        var guides = SvgEditorGuideService.Read(Document).Where(guide => guide.Id != id).ToArray();
        Execute("Delete guide", () => SvgEditorGuideService.Write(Document!, guides));
    }

    public void ClearGuides()
    {
        EnsureGuidesEditable();
        Execute("Clear guides", () => SvgEditorGuideService.Write(Document!, Array.Empty<SvgEditorGuide>()));
    }

    public void AddSelectionBoundsGuides()
    {
        EnsureGuidesEditable();
        if (SelectionBounds is not { } bounds)
            return;
        var guides = SvgEditorGuideService.Read(Document).ToList();
        foreach (var (origin, direction) in new[]
        {
            (new SKPoint(bounds.Left, bounds.Top), new SKPoint(0, 1)),
            (new SKPoint(bounds.Right, bounds.Top), new SKPoint(0, 1)),
            (new SKPoint(bounds.Left, bounds.Top), new SKPoint(1, 0)),
            (new SKPoint(bounds.Left, bounds.Bottom), new SKPoint(1, 0))
        })
            guides.Add(GuideInRootCoordinates(new SvgEditorGuide(Guid.NewGuid().ToString("N"), origin, direction)));
        Execute("Create selection bounds guides", () => SvgEditorGuideService.Write(Document!, guides));
    }

    private void EnsureGuidesEditable()
    {
        ThrowIfDisposed();
        if (Document is null)
            throw new InvalidOperationException("Open a document before editing guides.");
        if (IsLocked(Document))
            throw new InvalidOperationException("Unlock the document before editing guides.");
    }

    private SKMatrix GuideRootTransform()
    {
        var node = Document is null ? null : GetSceneNode(Document) ?? Renderer.RetainedSceneGraph?.Root;
        return node is null ? SKMatrix.Identity : ToMatrix(node.TotalTransform);
    }

    private SvgEditorGuide GuideInRootCoordinates(SvgEditorGuide guide)
    {
        if (!IsFinite(guide.Origin.X) || !IsFinite(guide.Origin.Y) || !SvgEditorGuideService.TryNormalize(guide.Direction, out _))
            throw new ArgumentException("Guide coordinates must be finite and the direction nonzero.", nameof(guide));
        if (!GuideRootTransform().TryInvert(out var inverse))
            throw new InvalidOperationException("The document viewport has a singular transform.");
        return TransformGuide(guide, inverse);
    }

    private static SvgEditorGuide TransformGuide(SvgEditorGuide guide, SKMatrix transform)
    {
        var origin = transform.MapPoint(guide.Origin);
        // Transform a vector without subtracting large translated coordinates.
        var direction = new SKPoint(transform.ScaleX * guide.Direction.X + transform.SkewX * guide.Direction.Y,
            transform.SkewY * guide.Direction.X + transform.ScaleY * guide.Direction.Y);
        SvgEditorGuideService.TryNormalize(direction, out direction);
        return new SvgEditorGuide(guide.Id, origin, direction);
    }
}
