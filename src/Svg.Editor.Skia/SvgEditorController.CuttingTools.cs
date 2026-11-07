using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Cuts the closest editable path contour in picture coordinates. Closed contours
    /// open at the cut; open contours become two independently editable native paths.
    /// Selecting artwork restricts the tool to that selection and its descendants.
    /// </summary>
    public bool CutPathAt(SKPoint picturePoint, float tolerance = 8)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        if (!IsFinite(picturePoint.X) || !IsFinite(picturePoint.Y))
            throw new ArgumentOutOfRangeException(nameof(picturePoint));
        if (!IsFinite(tolerance) || tolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        SvgVisualElement? target = null;
        SvgPathCutPoint? nearest = null;
        SKPath? geometry = null;
        var inverse = SKMatrix.Identity;
        try
        {
            foreach (var element in GetCuttingTargets())
            {
                var node = GetSceneNode(element);
                if (node is null)
                    continue;
                var world = ToMatrix(node.TotalTransform);
                if (!world.TryInvert(out var local))
                    continue;
                using var candidate = CreateGeometryPath(element);
                if (candidate is null)
                    continue;
                candidate.Transform(world);
                var bounds = candidate.Bounds;
                if (picturePoint.X < bounds.Left - tolerance || picturePoint.X > bounds.Right + tolerance
                    || picturePoint.Y < bounds.Top - tolerance || picturePoint.Y > bounds.Bottom + tolerance)
                    continue;
                var point = SvgPathCuttingService.FindNearest(candidate, picturePoint, Math.Max(0.00001f, nearest?.Separation ?? tolerance));
                if (point is null)
                    continue;
                geometry?.Dispose();
                geometry = new SKPath(candidate);
                target = element;
                nearest = point;
                inverse = local;
            }
            if (target is null || nearest is null || geometry is null)
                return false;
            ValidateCuttingTarget(target);
            var pieces = SvgPathCuttingService.SplitAt(geometry, nearest.Value);
            try
            {
                if (pieces.Count == 0)
                    return false;
                var replacements = new List<SvgPath>();
                foreach (var piece in pieces)
                {
                    piece.Transform(inverse);
                    var path = CloneCutPath(target, piece, replacements.Count == 0);
                    replacements.Add(path);
                }
                CommitCuttingResults("Cut path", new[] { (target, replacements) });
                return true;
            }
            finally { foreach (var piece in pieces) piece.Dispose(); }
        }
        finally { geometry?.Dispose(); }
    }

    /// <summary>
    /// Subtracts a round picture-space brush from editable vector geometry in one undo
    /// entry. Open stroked artwork becomes a filled outline before subtraction, retaining
    /// its actual dash pattern, cap and join. Generated brush artwork is detached from its
    /// original centerline so later brush changes cannot regenerate the erased material.
    /// </summary>
    public bool EraseAlong(IReadOnlyList<SKPoint> points, float diameter)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        using var eraser = SvgPathCuttingService.CreateSweep(points, diameter);
        var changes = new List<(SvgVisualElement Source, List<SvgPath> Pieces)>();
        foreach (var element in GetCuttingTargets())
        {
            var node = GetSceneNode(element);
            if (node is null)
                continue;
            var bounds = node.TransformedBounds;
            if (!CutBoundsOverlap(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), eraser.Bounds))
                continue;
            ValidateCuttingTarget(element);
            var world = ToMatrix(node.TotalTransform);
            if (!world.TryInvert(out var inverse))
                throw new InvalidOperationException("An object with a singular transform cannot be erased.");
            using var geometry = CreateEraserGeometry(element, world, out var outlinedStroke);
            if (geometry is null || geometry.IsEmpty)
                continue;
            using var overlap = geometry.Op(eraser, SKPathOp.Intersect)
                ?? throw new InvalidOperationException("The eraser intersection could not be calculated.");
            if (overlap.IsEmpty || overlap.Bounds.IsEmpty)
                continue;
            using var result = geometry.Op(eraser, SKPathOp.Difference)
                ?? throw new InvalidOperationException("The erased path could not be calculated.");
            var replacements = new List<SvgPath>();
            if (!result.IsEmpty && !result.Bounds.IsEmpty)
            {
                using var normalized = result.ToWinding() ?? throw new InvalidOperationException("The erased path could not be normalized.");
                normalized.Transform(inverse);
                var path = CloneCutPath(element, normalized, true);
                if (outlinedStroke)
                {
                    path.Fill = element.Stroke;
                    path.FillOpacity = element.StrokeOpacity;
                    path.Stroke = SvgPaintServer.None;
                    path.StrokeWidth = 0;
                    path.StrokeDashArray = null;
                    path.MarkerStart = path.MarkerMid = path.MarkerEnd = new Uri("none", UriKind.Relative);
                }
                replacements.Add(path);
            }
            changes.Add((element, replacements));
        }
        if (changes.Count == 0)
            return false;
        CommitCuttingResults("Erase artwork", changes);
        return true;
    }

    /// <summary>
    /// Partitions filled artwork along a freehand knife stroke, preserving the union of
    /// both sides. Each side keeps its source's parent, local transform and presentation.
    /// No provisional stroke or partial result enters the document or its undo history.
    /// </summary>
    public bool KnifeAlong(IReadOnlyList<SKPoint> points)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        // Validates the input before inspecting selection and also gates distant paths:
        // extending the knife's end tangents must not cut artwork it never touched.
        using var probe = SvgPathCuttingService.CreateSweep(points, 0.002f);
        if (points.Count < 2)
            return false;
        var changes = new List<(SvgVisualElement Source, List<SvgPath> Pieces)>();
        foreach (var element in GetCuttingTargets())
        {
            if (element.Fill == SvgPaintServer.None || element is SvgLine)
                continue;
            var node = GetSceneNode(element);
            if (node is null)
                continue;
            var world = ToMatrix(node.TotalTransform);
            if (!world.TryInvert(out var inverse))
                throw new InvalidOperationException("An object with a singular transform cannot be cut.");
            using var source = CreateGeometryPath(element);
            if (source is null)
                continue;
            source.Transform(world);
            if (!CutBoundsOverlap(source.Bounds, probe.Bounds))
                continue;
            using var touched = source.Op(probe, SKPathOp.Intersect)
                ?? throw new InvalidOperationException("The knife intersection could not be calculated.");
            if (touched.IsEmpty || touched.Bounds.IsEmpty)
                continue;
            ValidateCuttingTarget(element);
            using var side = SvgPathCuttingService.CreateKnifeRegion(points, source.Bounds);
            using var first = source.Op(side, SKPathOp.Intersect)
                ?? throw new InvalidOperationException("The first knife region could not be calculated.");
            using var second = source.Op(side, SKPathOp.Difference)
                ?? throw new InvalidOperationException("The second knife region could not be calculated.");
            if (first.IsEmpty || first.Bounds.IsEmpty || second.IsEmpty || second.Bounds.IsEmpty)
                continue;
            var replacements = new List<SvgPath>();
            foreach (var piece in new[] { first, second })
            {
                using var normalized = piece.ToWinding() ?? throw new InvalidOperationException("A knife region could not be normalized.");
                normalized.Transform(inverse);
                replacements.Add(CloneCutPath(element, normalized, replacements.Count == 0));
            }
            changes.Add((element, replacements));
        }
        if (changes.Count == 0)
            return false;
        CommitCuttingResults("Knife artwork", changes);
        return true;
    }

    private SvgVisualElement[] GetCuttingTargets()
    {
        if (Document is not { } document)
            return Array.Empty<SvgVisualElement>();
        var selection = new HashSet<SvgElement>(_selection);
        return document.Descendants().OfType<SvgVisualElement>()
            .Where(element => element is SvgPath or SvgRectangle or SvgCircle or SvgEllipse or SvgLine or SvgPolygon or SvgPolyline)
            .Where(IsSelectableArtwork).Where(element => !IsEditLocked(element))
            .Where(element => selection.Count == 0 || selection.Contains(element) || HierarchyAncestors(element).Any(selection.Contains)).ToArray();
    }

    private static void ValidateCuttingTarget(SvgVisualElement element)
    {
        if (element is SvgLine line)
        {
            ValidateDivisionUnits(line.StartX, line.StartY, line.EndX, line.EndY);
            if (line.Children.Count != 0 || line.Filter is not null || line.ClipPath is not null
                || line.TryGetAttribute("mask", out var mask) && !string.IsNullOrWhiteSpace(mask) && mask != "none")
                throw new InvalidOperationException("Release clips, masks and effects before cutting this line.");
        }
        else
            ValidateDivisionOperand(element);
    }

    private SKPath? CreateEraserGeometry(SvgVisualElement element, SKMatrix world, out bool outlinedStroke)
    {
        outlinedStroke = element.Fill == SvgPaintServer.None || element is SvgLine;
        var geometry = CreateGeometryPath(element);
        if (geometry is null)
            return null;
        if (!outlinedStroke)
        { geometry.Transform(world); return geometry; }
        try
        {
            if (element.Stroke is null || element.Stroke == SvgPaintServer.None || element.StrokeWidth.Value <= 0)
                return null;
            ValidateDivisionUnits(element.StrokeWidth, element.StrokeDashOffset);
            var nonScaling = element.TryGetAttribute("vector-effect", out var effect) && effect == "non-scaling-stroke";
            if (nonScaling)
                geometry.Transform(world);
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = element.StrokeWidth.Value,
                StrokeCap = element.StrokeLineCap switch { SvgStrokeLineCap.Round => SKStrokeCap.Round, SvgStrokeLineCap.Square => SKStrokeCap.Square, _ => SKStrokeCap.Butt },
                StrokeJoin = element.StrokeLineJoin switch { SvgStrokeLineJoin.Round => SKStrokeJoin.Round, SvgStrokeLineJoin.Bevel => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter },
                StrokeMiter = element.StrokeMiterLimit
            };
            SKPathEffect? dash = null;
            try
            {
                if (element.StrokeDashArray is { Count: > 0 } units)
                {
                    foreach (var unit in units)
                        ValidateDivisionUnits(unit);
                    var intervals = units.Select(unit => unit.Value).ToArray();
                    if (intervals.Any(value => value < 0) || intervals.All(value => value == 0))
                        throw new InvalidOperationException("Resolve the invalid stroke dash pattern before erasing it.");
                    if (intervals.Length % 2 != 0)
                        intervals = intervals.Concat(intervals).ToArray();
                    dash = SKPathEffect.CreateDash(intervals, element.StrokeDashOffset.Value);
                    paint.PathEffect = dash;
                }
                using var builder = new SKPathBuilder();
                if (!paint.GetFillPath(geometry, builder))
                    throw new InvalidOperationException("The stroke outline could not be calculated.");
                var result = builder.Detach();
                if (!nonScaling)
                    result.Transform(world);
                return result;
            }
            finally { dash?.Dispose(); }
        }
        finally { geometry.Dispose(); }
    }

    private SvgPath CloneCutPath(SvgVisualElement source, SKPath geometry, bool keepId)
    {
        var data = PathService.ToSvgPathData(geometry);
        if (data.Length > 16 * 1024 * 1024)
            throw new InvalidOperationException("The cut result is too large. Cut a smaller selection.");
        var path = CloneAsPath(source, data, true);
        if (!keepId || string.IsNullOrEmpty(path.ID))
            path.ID = CreateId("cut");
        foreach (var attribute in new[] { SvgVariableStrokeService.CenterlineAttribute, SvgVariableStrokeService.OutlineAttribute,
            SvgVariableStrokeService.ProfileAttribute, SvgVariableStrokeService.CapAttribute, SvgBrushLibraryService.BrushIdAttribute,
            SvgBrushLibraryService.SettingsAttribute, SvgBrushLibraryService.BaseOpacityAttribute, "stroke-profile" })
            path.CustomAttributes.Remove(attribute);
        return path;
    }

    private void CommitCuttingResults(string label, IEnumerable<(SvgVisualElement Source, List<SvgPath> Pieces)> changes)
    {
        Execute(label, () =>
        {
            _selection.Clear();
            foreach (var (source, pieces) in changes)
            {
                var parent = source.Parent ?? throw new InvalidOperationException("The cut target no longer belongs to its document.");
                var index = parent.Children.IndexOf(source);
                parent.Children.Remove(source);
                foreach (var piece in pieces)
                {
                    parent.Children.Insert(index++, piece);
                    RebindPaintServers(piece);
                    _selection.Add(piece);
                }
            }
        });
    }

    private static bool CutBoundsOverlap(SKRect left, SKRect right)
        => left.Left <= right.Right && left.Right >= right.Left && left.Top <= right.Bottom && left.Bottom >= right.Top;
}
